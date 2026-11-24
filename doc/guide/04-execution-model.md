# 04 — The execution model

This is the most important chapter. Read it twice. It explains what actually happens when a workflow runs.

## The four execution paths (recap)

| Path | Service | Semantics |
|------|---------|-----------|
| Direct execution | `IWorkflowRunner` | In-process, immediate, no queue |
| Runtime client | `IWorkflowRuntime` | Managed persisted instances: start/run/resume/cancel |
| Background dispatch | `IWorkflowDispatcher` | Queued; workers execute; distributed |
| Stimulus delivery | `IStimulusSender`/`Dispatcher` | External events → start/resume |

## What happens when a workflow executes

1. **Execute or Dispatch** — entry point chosen.
2. **Load** — definition loaded (from code provider or store/cache).
3. **Instantiate** — a `WorkflowInstance` is created, or an existing one is resumed from a bookmark.
4. **Execute** — activities run in bursts: schedule → evaluate inputs → run → process outcomes → schedule next.
5. **Bookmark (optional)** — if an activity creates a bookmark and the internal queue empties, state is persisted and execution suspends.
6. **Complete or Suspend** — instance status becomes `Completed`/`Finished` or `Suspended`.

This cycle repeats per burst.

## Execute vs Dispatch (the crucial distinction)

### Execute (`IWorkflowRunner`)

```csharp
// Run an inline workflow/activity directly:
var result = await workflowRunner.RunAsync(workflow);

// Resume an existing instance by id:
var result = await workflowRunner.RunAsync(new RunWorkflowOptions
{
    WorkflowInstanceId = instanceId,
    BookmarkId = bookmarkId,
    Input = input,
});
```

- Direct, synchronous, in-process.
- No queuing, no background workers.
- Does not persist unless you configure it and call commit (runner does commit state if persistence is configured).
- Use for tests, simple workflows, immediate results.

### Dispatch (`IWorkflowDispatcher`)

```csharp
await workflowDispatcher.DispatchAsync(
    new DispatchWorkflowDefinitionRequest { DefinitionId = workflowId });
```

- Asynchronous, queue-based.
- Request is enqueued; a background worker picks it up.
- Supports distributed execution across nodes.
- Default for production.

**Dispatch four request types:**

| Request | Purpose |
|---------|---------|
| `DispatchWorkflowDefinitionRequest` | Start new instance from a definition |
| `DispatchWorkflowInstanceRequest` | Continue an existing instance |
| `DispatchTriggerWorkflowsRequest` | Start instances from a trigger stimulus |
| `DispatchResumeWorkflowsRequest` | Resume instances waiting on bookmarks |

### Transactional outbox

When dispatch happens *during* workflow execution and the outbox is enabled, `TransactionalWorkflowDispatcher` writes the dispatch to Elsa's **workflow dispatch outbox** first, then hands off after state commit. This guarantees a dispatch is not lost if the process crashes mid-burst, and that downstream work only starts after the state that justifies it is persisted. Configure via runtime coordination storage (chapter 12).

### `IWorkflowRuntime`

High-level abstraction combining runtime management with persistence. Exposes a client API (`IWorkflowClient`) for start/run/resume/cancel/query. **Most application code should use this** rather than calling runner/dispatcher directly. See chapter 10.

## Bursts, scheduling, and the activity queue

Internally, the engine maintains an **activity execution queue**. A burst:

1. Enqueue the root activity.
2. Dequeue an activity; create its `ActivityExecutionContext`.
3. Evaluate input expressions (variables, outputs, workflow input).
4. Execute the activity logic.
5. Process outcomes → enqueue follow-up activities.
6. Repeat until queue empty.
7. If a bookmark was created during the burst → commit state, suspend.
8. Else → commit state, complete.

Side effects inside an activity happen during the burst. If the process crashes mid-burst, the burst is not checkpointed mid-way (only at burst boundaries). Design activities to be idempotent where possible; for downstream work, dispatch a child workflow instead of doing it inline — with the transactional outbox enabled (`WorkflowDispatcherOptions.UseTransactionalOutbox`), dispatches made during a burst are written to the outbox and only processed after state commit, so they survive a crash and never run ahead of the persisted state:

```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    var dispatcher = context.GetRequiredService<IWorkflowDispatcher>();
    var orderId = context.Get(OrderId);

    // Captured by the outbox when enabled: persisted with the burst,
    // dispatched only after commit.
    await dispatcher.DispatchAsync(new DispatchWorkflowDefinitionRequest(downstreamDefinitionVersionId)
    {
        CorrelationId = orderId,
        Input = new Dictionary<string, object> { ["OrderId"] = orderId },
    });

    await context.CompleteActivityAsync();
}
```

If you must perform a non-dispatch side effect directly, make it idempotent or guard it with persisted state.

## Triggers, bookmarks, stimuli (the event model)

### Triggers start new instances

Elsa indexes triggers from **published** definitions via `TriggerIndexer`. Two conditions:

- Activity implements `ITrigger`.
- Activity marked `CanStartWorkflow = true`.

Stored trigger record: definition/version IDs, activity ID, trigger name, payload, deterministic hash.

Publishing makes Elsa index start triggers. Unpublishing/re-publishing reindexes. **If a workflow doesn't start from an expected trigger, first check `CanStartWorkflow`.**

### Bookmarks pause existing instances

Bookmarks belong to instances, not definitions. Created via `ActivityExecutionContext.CreateBookmark(...)`:

- Captures bookmark name, payload, hash, activity ID, activity node ID, activity instance ID.
- Added to execution context; persisted at commit.

`CreateBookmark` hashes the bookmark using name + payload (+ optionally activity instance ID). **Resume payload shape must match what was stored**, or the hash lookup misses.

Bookmarks are for: HTTP callbacks, approvals, events/signals, timers, message arrivals, background task completion.

### Stimuli start or resume

`StimulusSender` first tries to start new workflows (if stimulus not scoped to an existing instance/activity), then tries to resume matching bookmarks. **One event can do both** — start new instances and resume suspended ones.

If no bookmark matches, Elsa enqueues the stimulus into the **bookmark queue** (reliability). The recurring `TriggerBookmarkQueueRecurringTask` keeps signaling the queue worker so bookmarks created slightly later can still pick up early stimuli. This is why an early callback rarely gets lost.

### Resume matching

`WorkflowResumer`:

1. Builds a bookmark filter (by bookmark ID or hashed stimulus data).
2. Acquires a **distributed lock** for that filter.
3. Loads matching bookmarks.
4. Creates a workflow client per instance.
5. Runs the instance from the matched bookmark.

The lock matters in clustered deployments (avoids duplicate resumes).

## What gets persisted (commit)

`DefaultCommitStateHandler` commits runtime state. On commit:

- Bookmark changes
- Activity execution logs
- Workflow execution logs
- Persisted variables
- The workflow instance state itself

After save, emits `WorkflowStateCommitted` — the hook for deferred dispatch outbox processing.

**Implication:** for workflows to survive restarts/resumes/callbacks, configure **runtime persistence** (EF Core etc.), not in-memory-only.

## Direct vs runtime vs dispatch — choosing

| Need | Use |
|------|-----|
| Run immediately in process | `IWorkflowRunner` |
| Start/resume managed persisted instances from app code | `IWorkflowRuntime` client API |
| Queue for background execution | `IWorkflowDispatcher` |
| React to external events | trigger + stimulus delivery |
| Pause and continue later | bookmark-based activity |

## Common mistakes

- Treating every blocking activity as a start trigger. Start triggers must be `ITrigger` **and** `CanStartWorkflow`.
- In-memory-only runtime for workflows that must survive restarts.
- Resume payload shape mismatch → hash miss → no resume.
- Assuming dispatch = executed. Dispatch = queued successfully.
- Expecting unpublished definitions to receive trigger traffic.

## Next

Continue with [05 — Variables, inputs & outputs](05-variables-inputs-outputs.md).
