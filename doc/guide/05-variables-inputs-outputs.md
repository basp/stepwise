# 05 — Variables, inputs & outputs

Data flow is the thing beginners trip on most. Elsa has three overlapping concepts: **variables**, **activity inputs/outputs**, and **workflow inputs/outputs**. This chapter separates them.

## Activity input

An activity's public property. Set in object initializer:

```csharp
new WriteLine
{
    Text = new Input<string>("Hello"),          // literal
};
new HttpEndpoint
{
    Path = new Input<string>("/echo"),          // literal
};
```

Inputs can be:

- **Literal**: `new Input<string>("value")`.
- **Variable reference**: `new Input<string>(myVariable)` (auto-reads).
- **Delegate**: `new Input<T>(context => ...)` (evaluated at execution time).
- **Expression**: via `Input<T>` with an expression string + language.

## Activity output

Data produced by an activity. Access in subsequent activities via the activity instance or `ActivityOutput`:

```csharp
var endpoint = new HttpEndpoint { Path = new("/x"), CanStartWorkflow = true };
// later:
new SetVariable
{
    Variable = myVar,
    Value = new(context => endpoint.GetOutput<HttpRequestData>(context)) // pseudo
};
```

More commonly, outputs auto-bind to variables (see below).

## Variable

Workflow-level (or scoped) named storage. Declared on the builder:

```csharp
var messageVariable = builder.WithVariable<string>();
var queryStringsVariable = builder.WithVariable<IDictionary<string, object>>();
```

Or inline via `Variable<T>` — useful for scoping or for variables you want to construct explicitly:

```csharp
var counter = new Variable<int>("Counter", 0);
```

An inline variable is **not** registered anywhere by itself. It only becomes part of the workflow/activity once you add it to a variable container's `Variables` collection. Register it at workflow level:

```csharp
var counter = new Variable<int>("Counter", 0);
builder.WithVariable(counter);   // equivalent: builder.Variables.Add(counter);
```

Or scope it to a container (`Sequence`, `Flowchart`, `Composite`, `Parallel`, …), where it is visible to that container's activities and its descendants:

```csharp
builder.Root = new Sequence
{
    Variables = { counter },
    Activities =
    {
        new WriteLine { Text = new(context => counter.Get(context).ToString()) }
    }
};
```

`builder.WithVariable<T>(...)` (the first form above) does both steps for you — it creates the variable **and** registers it on the builder. Use `new Variable<T>()` when you need an explicit instance, e.g. to scope it to a branch, but remember to register it.

Read/write:

```csharp
// write
new SetVariable { Variable = messageVariable, Value = new(context => "hi") };

// read in a delegate
new WriteLine { Text = new(context => messageVariable.Get(context)) };
```

### Auto-binding outputs to variables

Many activities let you specify a variable to receive their output:

```csharp
new HttpEndpoint
{
    Path = new("/hello-world"),
    CanStartWorkflow = true,
    QueryStringData = new(queryStringsVariable)   // output → variable
},
```

This is the cleanest data-flow style: declare variables, bind activity outputs to them, read them in later delegates.

### Scoped variables

Variables can be scoped to a `Sequence`/`Flowchart`/branch (any `IVariableContainer`: `Sequence`, `Flowchart`, `Composite`, `Parallel`, …). A variable declared inside a scope is only visible there. To scope a variable, create it with `new Variable<T>()` and add it to that container's `Variables` collection (e.g. `new Sequence { Variables = { myVar }, ... }`); an inline variable that is never added to a `Variables` collection is not visible anywhere. Use scoping to avoid name collisions in parallel branches.

```csharp
// A variable scoped to a single branch of a Parallel — invisible to the other branch.
var branchAVar = new Variable<int>("BranchAValue", 0);

builder.Root = new Parallel
{
    Branches =
    {
        new Sequence
        {
            Variables = { branchAVar },
            Activities =
            {
                new SetVariable { Variable = branchAVar, Value = new(context => 42) },
                new WriteLine { Text = new(context => branchAVar.Get(context).ToString()) }  // 42
            }
        },
        new Sequence
        {
            Activities =
            {
                // branchAVar is not visible here — different scope
                new WriteLine { Text = new("other branch") }
            }
        }
    }
};
```

## Workflow input

Data passed in when starting an instance. Declared:

```csharp
var orderIdInput = builder.WithInput<string>("OrderId");
```

Supplied at start time:

```csharp
await runtime.RunAsync(definitionId, new Dictionary<string, object>
{
    ["OrderId"] = "12345"
});
```

Or via dispatch:

```csharp
new DispatchWorkflowDefinitionRequest(definitionVersionId)
{
    Input = new Dictionary<string, object> { ["OrderId"] = orderId }
};
```

Inside the workflow, read via the variable reference `orderIdInput` or via `context.GetWorkflowInput<T>("OrderId")`.

### `HttpEndpoint` triggers and workflow inputs

An `HttpEndpoint` with `CanStartWorkflow = true` starts an instance from an HTTP request, but it does **not** populate your declared workflow inputs. It captures the request through its own outputs (`QueryStringData`, `RouteData`, `Headers`, `ParsedContent`, …), which you bind to **variables** — see the data-flow example below. Workflow inputs are only supplied by the caller when starting an instance programmatically (via `runtime.RunAsync`/`DispatchWorkflow`).

```csharp
var routeDataVariable = builder.WithVariable<IDictionary<string, object>>();

builder.Root = new Sequence
{
    Activities =
    {
        new HttpEndpoint
        {
            Path = new("/orders/{orderId}"),
            CanStartWorkflow = true,
            RouteData = new(routeDataVariable),   // request data → variable
        },
        new WriteLine { Text = new(context => routeDataVariable.Get(context)!["orderId"]?.ToString()) }
    }
};
```

If you do need to start a *different* workflow with workflow inputs derived from an HTTP request, build the dictionary and dispatch it (requires `Elsa.Workflows.Runtime`):

```csharp
new DispatchWorkflow
{
    WorkflowDefinitionId = new("ChildWorkflow"),
    Input = new(context => new Dictionary<string, object>
    {
        ["OrderId"] = routeDataVariable.Get(context)!["orderId"]!
    })
};
```

## Workflow output

Data returned when the workflow completes:

```csharp
var resultOutput = builder.WithOutput<int>("Result");
// set it somewhere:
new SetOutput { Output = resultOutput, Value = new(context => 42) };
```

Read from the run result / instance after completion.

## Expressions vs delegates (code-first guidance)

| Style | When |
|-------|------|
| Delegate `new Input<T>(ctx => ...)` | Code-first workflows. Type-safe, refactorable, debuggable. |
| Expression string | JSON/designer workflows, or dynamic user-authored logic. |

In code-first, **prefer delegates**. They compile, refactor, and are testable. Use expression strings only when the logic must be data-driven.

## Common expression variables (if you use strings)

- `WorkflowInstanceId`, `CorrelationId`
- `Variables` (dictionary)
- `ActivityOutput` / specific activity outputs
- `Input` (workflow input)
- `Environment`, `Configuration`

See [C# expressions](https://docs.elsaworkflows.io/expressions/c.md) and [JavaScript](https://docs.elsaworkflows.io/expressions/javascript.md).

## Data flow example (from sample `HttpHelloWorld`)

```csharp
var queryStringsVariable = builder.WithVariable<IDictionary<string, object>>();
var messageVariable = builder.WithVariable<string>();

builder.Root = new Sequence
{
    Activities =
    {
        new HttpEndpoint
        {
            Path = new("/hello-world"),
            CanStartWorkflow = true,
            QueryStringData = new(queryStringsVariable),   // 1. capture query
        },
        new SetVariable
        {
            Variable = messageVariable,                    // 2. compute
            Value = new(context =>
            {
                var qs = queryStringsVariable.Get(context)!;
                return qs.TryGetValue("message", out var v) ? v?.ToString() : "Hello world!";
            })
        },
        new WriteHttpResponse
        {
            Content = new(messageVariable)                 // 3. respond
        }
    }
};
```

Pattern: **capture → transform → emit**, all via variables. This is the idiomatic Elsa data-flow style.

## Tips

- Declare variables at the top of `Build` so they're visible everywhere.
- Use `WithVariable<T>()` for discoverability and automatic registration; use `new Variable<T>()` for scoping/local use, but always add it to a container's `Variables` collection (workflow builder or a scoped container) — an unregistered variable is invisible to the engine.
- Don't put heavy logic in delegates that run on every evaluation — inputs are evaluated when the activity executes, but some may be evaluated multiple times. Keep delegates pure and cheap.
- For complex transformations, write a custom activity (chapter 09) instead of a giant delegate.

## Next

Continue with [06 — Control flow](06-control-flow.md).
