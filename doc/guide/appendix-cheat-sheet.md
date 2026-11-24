# Appendix — Cheat sheet

## Packages (3.10.2)

```
Elsa
Elsa.Http
Elsa.Scheduling
Elsa.Persistence.EFCore.Sqlite    // or SqlServer, PostgreSql, MySql (choose one)
Elsa.Expressions.JavaScript / .CSharp / .Liquid
Elsa.OpenTelemetry
```

Keep all `Elsa.*` packages on the same version. Elsa 3.10.2 MySQL is not supported on .NET 11; see chapter 08.

## Host wiring

```csharp
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Elsa")!;

builder.Services.AddElsa(elsa =>
{
    // elsa.AddWorkflow<MyWorkflow>();
    // elsa.UseScheduling();
    // elsa.UseHttp(...);

    elsa.UseWorkflowManagement(management =>
        management.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlite(connectionString);
            ef.RunMigrations = false; // Apply migrations in the deployment pipeline.
        }));

    elsa.UseWorkflowRuntime(runtime =>
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlite(connectionString);
            ef.RunMigrations = false; // Apply migrations in the deployment pipeline.
        }));
});

var app = builder.Build();
// app.UseWorkflows(); // when Elsa's HTTP/API features are enabled
app.Run();
```

## Workflow skeleton

```csharp
internal class MyWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        var v = builder.WithVariable<string>();
        var i = builder.WithInput<string>("In");
        var o = builder.WithOutput<int>("Out");
        builder.Root = new Sequence { Activities = { ... } };
    }
}
```

## Core types

- `Workflow`, `WorkflowInstance`, `WorkflowBase`, `IWorkflowBuilder`
- `IActivity`, `Activity`, `CodeActivity`, `CodeActivity<T>`
- `Input<T>`, `Output<T>`, `Variable<T>`
- `IWorkflowRunner`, `IWorkflowRuntime`, `IWorkflowDispatcher`, `IStimulusSender`
- `ActivityExecutionContext`, `WorkflowExecutionContext`

## Execution requests

```csharp
new RunWorkflowOptions { WorkflowInstanceId, BookmarkId, CorrelationId, Input }  (IWorkflowRunner)
new DispatchWorkflowDefinitionRequest(definitionVersionId) { CorrelationId, Input }
new DispatchWorkflowInstanceRequest(instanceId)
new DispatchTriggerWorkflowsRequest(activityTypeName, bookmarkPayload)
new DispatchResumeWorkflowsRequest(activityTypeName, bookmarkPayload)
```

## Common activities

`Sequence`, `Flowchart`, `Parallel`, `Fork`, `Join`, `If`, `Switch`, `While`, `ForEach`, `ParallelForEach`, `TryCatch`, `Delay`, `Event`, `Timer`, `HttpEndpoint`, `WriteHttpResponse`, `WriteLine`, `SetVariable`, `SetOutput`, `Correlate`, `RunWorkflow`, `DispatchWorkflow`, `UserTask`, `Code`.

## Statuses

- `WorkflowStatus`: `Running`, `Finished`.
- `WorkflowSubStatus`: `Pending`, `Executing`, `Suspended`, `Finished`, `Cancelled`, `Faulted`, `Interrupted`.
- `ActivityStatus`: `Pending`, `Running`, `Completed`, `Canceled`, `Faulted`.

## Patterns

- **HTTP request/response**: `HttpEndpoint` (trigger) → work → `WriteHttpResponse`.
- **Event or timeout**: `Fork(WaitAny)` with event branch + `Delay` branch.
- **Correlation**: `Correlate` + pass `?correlationId=`; resume with same.
- **Child workflow**: `RunWorkflow` (inline) vs `DispatchWorkflow` (async).
- **Capture→transform→emit**: bind outputs to variables, read in delegates.

## Rules of thumb

1. Author/test with `IWorkflowRunner`; operate with `IWorkflowRuntime`/`IWorkflowDispatcher`.
2. Prefer delegates over expression strings in code-first.
3. Always pair event-waits with a timeout (`Fork` + `Delay`).
4. Always set a correlation ID on resumable instances.
5. Make side-effecting activities idempotent.
6. Use durable persistence + durable scheduler + distributed locks in prod.
7. Configure retention.
8. `CanStartWorkflow = true` + `ITrigger` = start trigger.
9. Resume payload shape must match bookmark payload shape.
10. Dispatch ≠ executed (it's queued).

## Official doc map

- Concepts: https://docs.elsaworkflows.io/getting-started/concepts.md
- Architecture: https://docs.elsaworkflows.io/guides/architecture.md
- Execution model: https://docs.elsaworkflows.io/guides/architecture/execution-model.md
- Hello World: https://docs.elsaworkflows.io/getting-started/hello-world.md
- Hosting in existing app: https://docs.elsaworkflows.io/guides/onboarding/hosting-elsa-in-existing-app.md
- Custom activities: https://docs.elsaworkflows.io/extensibility/custom-activities.md
- Persistence: https://docs.elsaworkflows.io/guides/persistence.md
- Long-running: https://docs.elsaworkflows.io/guides/running-workflows/long-running-workflows.md
- Clustering: https://docs.elsaworkflows.io/guides/clustering.md
- Incidents: https://docs.elsaworkflows.io/operate/incidents.md
- Activity reference: https://docs.elsaworkflows.io/activities/activity-reference.md
- llms.txt (full index): https://docs.elsaworkflows.io/llms.txt
