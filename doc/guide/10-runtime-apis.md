# 10 — Runtime APIs

When you need to start, resume, cancel, or inspect workflows from your application code (controllers, services, background jobs), use the runtime APIs — not the runner directly.

## `IWorkflowRuntime`

The high-level operational API. Obtain a client for a definition or instance:

```csharp
var runtime = serviceProvider.GetRequiredService<IWorkflowRuntime>();

// Start a new instance from a definition
var result = await runtime.StartWorkflowAsync(definitionId, new StartWorkflowRuntimeParams
{
    CorrelationId = correlationId,
    Input = new Dictionary<string, object> { ["OrderId"] = orderId },
});

// Resume a suspended instance
await runtime.ResumeWorkflowAsync(instanceId, new ResumeWorkflowRuntimeParams
{
    BookmarkId = bookmarkId,
    Input = input,
});

// Cancel
await runtime.CancelWorkflowAsync(instanceId, CancellationToken.None);
```

Common client operations:

- `StartWorkflowAsync(definitionId, params)` — start.
- `ResumeWorkflowAsync(instanceId, params)` — resume.
- `CancelWorkflowAsync(instanceId, ct)` — cancel.
- `FindWorkflowsAsync(filter, ct)` — query.

## `IWorkflowRunner` (low-level)

```csharp
var runner = sp.GetRequiredService<IWorkflowRunner>();

// Run an inline workflow object
var result = await runner.RunAsync(workflow);

// Resume an instance/bookmark
var result = await runner.RunAsync(new RunWorkflowOptions
{
    WorkflowInstanceId = instanceId,
    BookmarkId = bookmarkId,
    CorrelationId = correlationId,
    Input = new Dictionary<string, object> { ["OrderId"] = orderId },
});

```

Use the runner when you need fine-grained control or are writing tests. Use the runtime client for normal app code.

## `IWorkflowDispatcher` (queued)

```csharp
var dispatcher = sp.GetRequiredService<IWorkflowDispatcher>();

await dispatcher.DispatchAsync(new DispatchWorkflowDefinitionRequest(definitionVersionId)
{
    CorrelationId = orderId,
    Input = new Dictionary<string, object> { ["OrderId"] = orderId },
});

await dispatcher.DispatchAsync(new DispatchResumeWorkflowsRequest(
    activityTypeName: "HttpEndpoint",
    bookmarkPayload: new { Path = "/finish" }));

await dispatcher.DispatchAsync(new DispatchWorkflowInstanceRequest(instanceId));
```

Dispatch returns after enqueueing — the work happens later on a worker. Don't expect the instance to be finished when `DispatchAsync` returns.

## Starting from a trigger (stimulus)

```csharp
var sender = sp.GetRequiredService<IStimulusSender>();
await sender.SendAsync("OrderPaid", new { OrderId = orderId }, correlationId: correlationId);
```

This both starts new instances whose trigger matches and resumes suspended bookmarks.

## Querying instances

```csharp
var store = sp.GetRequiredService<IWorkflowInstanceStore>();
var instances = await store.FindManyAsync(new WorkflowInstanceFilter
{
    DefinitionId = definitionId,
    Status = WorkflowStatus.Suspended,
    CorrelationId = correlationId,
}, cancellationToken);
```

Or via the management REST API (`Elsa.Workflows.Api`) if you expose it. The API is useful for Studio and external tooling.

## Cancelling / faulting

```csharp
await client.CancelAsync();   // graceful cancel → status Cancelled
```

Forcing a fault is usually done by throwing inside an activity or via alterations.

## Alterations (modifying a running instance)

Alterations let you change a suspended/running instance's state, variables, or schedule activities — useful for migrations and ops:

```csharp
// via IWorkflowInstanceManager / alteration API
```

See [Alterations](https://docs.elsaworkflows.io/features/alterations.md). Also exposed via REST API.

## Variables at runtime

Inspect/update variables of an instance:

```csharp
var vm = sp.GetRequiredService<IWorkflowInstanceVariableManager>();
var vars = await vm.GetVariablesAsync(instanceId, null, CancellationToken.None);
await vm.SetVariablesAsync(instanceId, newVariables, CancellationToken.None);
```

See [Variables](https://docs.elsaworkflows.io/operate/workflow-instance-variables.md).

## Definition management (stored definitions)

If you store definitions (not code-only), use `IWorkflowDefinitionManager` / `IWorkflowDefinitionStore` to create/version/publish. Publishing indexes triggers. Code-first definitions are registered at startup and treated as published.

## Choosing the right API

| Task | API |
|------|-----|
| Run a workflow now (test/script) | `IWorkflowRunner` |
| Start/manage persisted instances from app code | `IWorkflowRuntime` client |
| Queue work (prod, distributed) | `IWorkflowDispatcher` |
| React to external events | `IStimulusSender` |
| Inspect/query | `IWorkflowInstanceStore` + filters |
| Modify a running instance | Alterations |
| Manage definitions | `IWorkflowDefinitionManager` |

## Common pitfalls

- Calling `IWorkflowRunner` in production request handlers → blocks the request and bypasses queueing/distribution. Use dispatch.
- Forgetting that dispatch is async: checking instance status immediately after returns "not started yet."
- Resuming with wrong bookmark payload shape → silent no-op.
- Not setting `CorrelationId` → can't target instances later.

## Next

Continue with [11 — Testing & debugging](11-testing-debugging.md).
