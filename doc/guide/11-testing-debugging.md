# 11 — Testing & debugging

Elsa workflows are testable because they're just object graphs + DI. This chapter covers the practical approach.

## Test setup

```csharp
[Fact]
public async Task EchoWorkflow_runs()
{
    var services = new ServiceCollection();
    services.AddElsa(elsa => { });
    var sp = services.BuildServiceProvider();
    var runner = sp.GetRequiredService<IWorkflowRunner>();

    var workflow = new Sequence
    {
        Activities = { new WriteLine("hi") }
    };

    var result = await runner.RunAsync(workflow);
    Assert.Equal(WorkflowStatus.Finished, result.WorkflowState.Status);
}
```

Use the in-memory (default) persistence in tests. No database needed for unit tests of workflow logic.

## Testing custom activities

Wrap the activity in a tiny workflow and run it:

```csharp
[Fact]
public async Task GetOrder_returns_order()
{
    var services = new ServiceCollection();
    services.AddElsa(elsa => { });
    services.AddSingleton<IOrderService, FakeOrderService>();
    var sp = services.BuildServiceProvider();
    var runner = sp.GetRequiredService<IWorkflowRunner>();

    var activity = new GetOrder { OrderId = new("42") };
    var workflow = new Sequence { Activities = { activity } };

    await runner.RunAsync(workflow);
    var output = activity.GetOutput<Order>(/* context */); // or inspect via variable
}
```

Better: bind the activity output to a variable and assert on the instance's variables after run.

## Testing long-running (bookmarks)

For suspended workflows, assert status = `Suspended` and that a bookmark exists:

```csharp
var result = await runner.RunAsync(workflow);
Assert.Equal(WorkflowSubStatus.Suspended, result.WorkflowState.SubStatus);
Assert.NotEmpty(result.WorkflowState.Bookmarks);
```

Then resume via `runner.RunAsync(new RunWorkflowOptions { WorkflowInstanceId = ..., BookmarkId = ... })` and assert `Finished`.

## Testing with persistence

For integration tests that need real persistence, use SQLite in-memory or a test container. Keep unit tests in-memory.

## Debugging techniques

### 1. Execution journal / logs

Elsa records activity execution logs and workflow execution logs. Inspect via:

- `IWorkflowInstanceStore` + `IActivityExecutionStore`.
- Studio's instance view (if hosted).
- `IWorkflowExecutionLogStore`.

Each entry: activity id, status, start/end time, inputs/outputs (if captured), exception.

### 2. Enable structured logging

```csharp
builder.Logging.AddFilter("Elsa", LogLevel.Debug);
```

Elsa logs scheduling, dispatch, bookmark matching, and commit steps. Very useful for "why didn't it resume?".

### 3. Incidents

When an activity faults, an incident is recorded with the exception. Inspect:

```csharp
var instance = await store.FindByIdAsync(instanceId);
foreach (var inc in instance.Incidents) { /* inc.Message, inc.StackTrace */ }
```

Strategies (retry/suspend/fail) are configurable per activity/host. See [Incidents](https://docs.elsaworkflows.io/operate/incidents.md).

### 4. Breakpoints in delegates

Delegates run in your process — set breakpoints inside `context => ...` lambdas. Activities' `ExecuteAsync` are also debuggable.

### 5. Activity execution records (Studio)

Studio shows per-activity execution evidence (inputs/outputs captured). Enable via `Elsa` execution log persistence (default with persistence).

### 6. OpenTelemetry tracing

Elsa emits spans for workflow/activity execution. Wire `Elsa.OpenTelemetry` + an OTLP collector (Jaeger/Zipkin/Aspire dashboard) for distributed traces. See [OpenTelemetry Tracing](https://docs.elsaworkflows.io/extensibility/opentelemetry-tracing.md).

## Common failure modes & fixes

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Workflow doesn't start on HTTP hit | Trigger not indexed (not published / `CanStartWorkflow` false) | Check `CanStartWorkflow = true`, app started fully |
| Resume does nothing | Bookmark payload hash mismatch | Match payload shape exactly; check logs |
| Instance stuck Suspended | No stimulus arriving; or early-arrival not yet processed | Wait for queue task; check correlation; manual resume |
| Duplicate execution in cluster | Missing distributed lock | Configure lock provider |
| Timers don't fire after restart | In-memory scheduler | Use Quartz/Hangfire |
| DB grows unbounded | No retention | Configure retention |
| Activity re-runs after crash | Burst boundary semantics | Make activities idempotent |
| Variables null after resume | Not persisted (not marked persisted) | Mark variable persisted |

## Performance testing

Use the throughput-tuning guide: measure commit/worker/outbox tradeoffs under load. See [Throughput Tuning](https://docs.elsaworkflows.io/guides/performance/throughput-tuning.md). Key knobs: worker count, commit batch size, outbox enabled.

## Next

Continue with [12 — Operating in production](12-production.md).
