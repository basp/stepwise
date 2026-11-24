# 03 — Authoring workflows in code

Elsa workflows are just object graphs. There is no XML, no code generation step, and no special compiler. You build a tree of activity instances and assign it to a workflow's `Root`.

## The three authoring styles

1. **Code-first classes** (recommended for products): a class deriving from `WorkflowBase` overriding `Build(IWorkflowBuilder)`. Version-controlled, testable, refactorable. This is what the sample project uses.
2. **Inline object graphs**: build a `Sequence { ... }` directly and run it via `IWorkflowRunner`. Great for tests and scripts.
3. **JSON / ElsaScript**: load definitions from files/storage. Useful for user-authored or dynamically deployed workflows. See [Loading Workflows from JSON](https://docs.elsaworkflows.io/guides/loading-workflows-from-json.md).

This guide focuses on (1) and (2).

## `WorkflowBase`

```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;

internal class EchoWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new Input<string>("/echo"),
                    CanStartWorkflow = true,
                },
                new WriteLine("Hello!"),
            },
        };
    }
}
```

Key points:

- `Build` is called once when the definition is created/cached. It constructs the activity tree.
- `builder.Root` is the entry point activity. Almost always a `Sequence` or `Flowchart`.
- Activities are plain objects with object-initializer syntax. Inputs are set via `Input<T>` (see chapter 05).
- `CanStartWorkflow = true` on an `HttpEndpoint` makes it a **trigger** that starts new instances.

## `IWorkflowBuilder` helpers

The builder gives you conveniences:

```csharp
var myVar = builder.WithVariable<string>();        // declare a workflow variable
var input  = builder.WithInput("OrderId", typeof(string)); // declare a workflow input
var output = builder.WithOutput("Result", typeof(int));    // declare a workflow output
```

Variables declared this way are typed and discoverable by Studio/expressions.

## Common building-block activities

| Activity | Purpose |
|----------|---------|
| `Sequence` | Run children in order |
| `Flowchart` | Graph with explicit connections/branches |
| `Parallel` / `Fork` / `Join` | Concurrency |
| `If` / `Decision` (via outcomes) | Branching |
| `While` / `ForEach` | Loops |
| `Delay` | Wait for a duration (bookmark) |
| `Event` | Wait for a named event (bookmark) |
| `HttpEndpoint` | Receive HTTP (trigger or bookmark) |
| `WriteHttpResponse` | Send HTTP response |
| `WriteLine` | Console/log output |
| `SetVariable` | Assign a variable |
| `Correlate` | Set correlation ID |
| `RunWorkflow` / `DispatchWorkflow` | Start child workflows |
| `Code` / `Inline` | Run arbitrary C#/JS |

Full list: [Activity Reference](https://docs.elsaworkflows.io/activities/activity-reference.md).

## A richer example: the `Test` workflow

The sample project's `Test` workflow demonstrates most concepts:

```csharp
internal class Test : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint { Path = new Input<string>("/test"), CanStartWorkflow = true },
                new Correlate(context =>
                {
                    var wc = context.GetWorkflowExecutionContext();
                    return string.IsNullOrWhiteSpace(wc.CorrelationId)
                        ? Guid.NewGuid().ToString("N")
                        : wc.CorrelationId;
                }),
                new WriteHttpResponse
                {
                    Content = new Input<object?>(context => new
                    {
                        correlationId = context.GetWorkflowExecutionContext().CorrelationId,
                    }),
                },
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAny,
                    Branches =
                    {
                        new Sequence  // branch 1: wait for /finish
                        {
                            Activities =
                            {
                                new HttpEndpoint { Path = new Input<string>("/finish"), CanStartWorkflow = false },
                                new WriteLine("OK."),
                            },
                        },
                        new Sequence  // branch 2: timeout
                        {
                            Activities =
                            {
                                new Delay(TimeSpan.FromSeconds(60)),
                                new WriteLine("Timeout."),
                            },
                        },
                    },
                },
                new WriteLine("Completed."),
            },
        };
    }
}
```

Notice:

- `Correlate` sets the instance's correlation ID from the caller's `?correlationId=` or `X-Correlation-ID`, or generates one. This is how you later target this instance.
- `WriteHttpResponse` returns the correlation ID to the caller so they can resume later.
- `Fork` with `WaitAny` runs two branches; the first to complete (finish OR timeout) wins; the other is cancelled.
- The second `HttpEndpoint` has `CanStartWorkflow = false`: it does **not** start a new instance; it acts as a **bookmark** that resumes the existing instance (matched by correlation ID / bookmark hash).

This single workflow demonstrates triggers, bookmarks, correlation, concurrency, and composition.

## Workflow inputs & outputs (definition-level)

```csharp
protected override void Build(IWorkflowBuilder builder)
{
    var orderIdInput = builder.WithInput<string>("OrderId");
    var resultOutput = builder.WithOutput<int>("Result");

    builder.Root = new Sequence { /* use orderIdInput, set resultOutput */ };
}
```

Inputs are supplied when starting the instance; outputs are available after completion. See chapter 10 (runtime APIs).

## Inline authoring (tests/scripts)

```csharp
var workflow = new Sequence
{
    Activities =
    {
        new WriteLine("Hello World!"),
        new WriteLine("More than a one-liner!"),
    }
};
await workflowRunner.RunAsync(workflow);
```

No class needed. Great for tests and one-off scripts.

## Expressions

Activity inputs can be dynamic. Elsa evaluates expressions in several languages: **C#, JavaScript, Liquid, Python**. In code-first workflows you usually pass a delegate instead:

```csharp
new SetVariable
{
    Variable = messageVariable,
    Value = new(context =>
    {
        var qs = queryStringsVariable.Get(context)!;
        return qs.TryGetValue("message", out var v) ? v?.ToString() : "Hello!";
    })
};
```

Delegates (`Func<ActivityExecutionContext, T>`) are the code-first equivalent of expression strings. Prefer delegates in code; use expression strings in JSON/designer workflows.

Built-in variables available in expressions: `WorkflowInstanceId`, `CorrelationId`, `Variables`, `ActivityOutput`, `Input` (workflow input). See [C# expressions](https://docs.elsaworkflows.io/expressions/c.md) / [JavaScript](https://docs.elsaworkflows.io/expressions/javascript.md).

## Activity metadata: `Kind`, outcomes

- `Kind` metadata: `Action` (default), `Trigger`, `Job`. Determines whether an activity can start instances (trigger) or is schedulable (job).
- Outcomes: declared by the activity; you connect follow-up activities to them in `Flowchart` or via `Activity` references. In `Sequence`, children run in order regardless of outcome (outcome `Done`).

## Naming & identity

Activities can have an `Id`. If omitted, Elsa generates one. Stable IDs matter for: resuming bookmarks, alterations, and reading execution logs. For code-first, IDs are derived from position in many cases, but set them explicitly when you need to target an activity.

## Source control & versioning

Code-first definitions are versioned by your git history. Runtime versioning (definition versions in the store) applies mainly to stored/published definitions. For code-first, the definition is recreated per app version; instances created under older code versions may not resume cleanly if the tree changed. Plan migrations (alterations) for long-lived instances.

## Next

Continue with [04 — The execution model](04-execution-model.md).
