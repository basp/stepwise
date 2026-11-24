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

Or inline via `Variable<T>`:

```csharp
var counter = new Variable<int>("Counter", 0);
```

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

Variables can be scoped to a `Sequence`/`Flowchart`/branch. A variable declared inside a scope is only visible there. Use scoping to avoid name collisions in parallel branches.

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
- Use `WithVariable<T>()` for discoverability; use `new Variable<T>()` for scoping/local use.
- Don't put heavy logic in delegates that run on every evaluation — inputs are evaluated when the activity executes, but some may be evaluated multiple times. Keep delegates pure and cheap.
- For complex transformations, write a custom activity (chapter 09) instead of a giant delegate.

## Next

Continue with [06 — Control flow](06-control-flow.md).
