# 07 — Long-running & event-driven workflows

This is Elsa's superpower: workflows that pause for days waiting for a human click, a payment webhook, or a timer, then resume exactly where they left off. This chapter explains how.

## The pattern (revisited)

1. A **trigger** starts the instance (e.g. `HttpEndpoint` with `CanStartWorkflow = true`).
2. Activities run in a burst until a **blocking activity** creates a **bookmark**.
3. State is persisted; instance status = `Suspended`.
4. Later, an external event becomes a **stimulus** (HTTP callback, timer fire, message).
5. Elsa matches stimulus → bookmark, acquires a lock, resumes the instance from the bookmark, runs the next burst.

## Blocking activities (create bookmarks)

- `Event` — waits for a named event.
- `Delay` — waits for a duration (uses the scheduler).
- `HttpEndpoint` (when `CanStartWorkflow = false`) — waits for an HTTP request.
- `UserTask` / `UserTask`-style activities — human-in-the-loop.
- Custom activities calling `CreateBookmark`.

Not every "wait" is a bookmark — only those that call `CreateBookmark`. A `while` loop with a `Delay` inside creates bookmarks each iteration.

## Triggers (start new instances)

Mark an activity `CanStartWorkflow = true` and ensure it implements `ITrigger`:

```csharp
new HttpEndpoint
{
    Path = new Input<string>("/orders"),
    CanStartWorkflow = true,   // <-- trigger
}
new Timer
{
    Interval = TimeSpan.FromHours(1),
    CanStartWorkflow = true,
}
```

Triggers are indexed at publish time. **Publishing** is what makes triggers live. For code-first hosts, registration via `AddWorkflow<T>` + app startup effectively publishes.

## Correlation ID (targeting instances)

When multiple instances are suspended on the same bookmark type (e.g. many orders waiting for approval), you need to resume the *right* one. Use correlation IDs:

```csharp
new Correlate(context =>
{
    var wc = context.GetWorkflowExecutionContext();
    return string.IsNullOrWhiteSpace(wc.CorrelationId)
        ? Guid.NewGuid().ToString("N")
        : wc.CorrelationId;
}),
```

The caller passes `?correlationId=...` or `X-Correlation-ID` header on the first request; Elsa stores it on the instance. On the resume request, the caller passes the same correlation ID; Elsa matches the suspended instance.

Also usable: workflow input `CorrelationId` (Elsa sets it automatically if passed at start).

## Resuming via HTTP (the sample `Test` workflow)

Flow:

1. `POST /workflows/test` (no correlation) → server generates one, returns `{ correlationId }`. Instance now suspended at the `Fork` (both branches active: one waiting on `/finish`, one on a 60s delay).
2. Within 60s, `POST /workflows/finish?correlationId=<id>` → resumes branch 1 → prints "OK." → `Fork` completes (WaitAny) → cancels branch 2 → prints "Completed."
3. If no finish arrives, branch 2's `Delay` fires after 60s → prints "Timeout." → completes → "Completed."

This is the canonical long-running pattern: **trigger → do work → fork (event vs timeout) → join → continue**.

## Resuming programmatically

```csharp
// By stimulus
await workflowDispatcher.DispatchAsync(new DispatchResumeWorkflowsRequest(
    activityTypeName: "HttpEndpoint",          // bookmark-producing activity type/name
    bookmarkPayload: new { Path = "/finish" }  // must match stored payload shape
) { CorrelationId = correlationId });

// Or via runtime client (chapter 10)
await runtime.ResumeWorkflowAsync(instanceId, new ResumeWorkflowRuntimeParams { BookmarkId = bookmarkId, Input = input });
```

**Payload shape must match** the bookmark's stored payload, or the hash lookup misses and nothing resumes. This is the #1 long-running bug.

## Event activity (named events)

```csharp
new Event
{
    EventName = new Input<string>("OrderPaid"),
    CanStartWorkflow = false,   // resume existing
}
```

Start a workflow with an `Event` trigger (`CanStartWorkflow = true`) to begin on that event. Use `IEventPublisher`/stimulus to fire events:

```csharp
await stimulusSender.SendAsync("OrderPaid", payload, new StimulusMetadata { CorrelationId = correlationId });
```

## Timers & scheduling

- `Delay` — one-shot wait for a duration (bookmark).
- `Timer` / `Cron` / `Interval` — recurring triggers (start new instances).
- Backed by `Elsa.Scheduling` (in-memory) or Quartz/Hangfire (durable) — see chapter 12.

Use durable schedulers (Quartz/Hangfire) in production so timers survive restarts.

## Bookmarks: create your own

In a custom activity:

```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    // ... do work ...
    context.CreateBookmark(new CreateBookmarkArgs
    {
        BookmarkName = "AwaitApproval",
        Payload = new { RequestId = requestId },
        Callback = OnResume,   // optional
    });
}

private async ValueTask OnResume(ActivityExecutionContext context)
{
    // runs when resumed
}
```

`CreateBookmark` hashes name+payload (+instance id if configured). Store enough in the payload to identify the instance and validate the stimulus.

## Stimulus routing (early arrival)

Stimuli can arrive before the bookmark exists (race). Elsa handles this:

1. `StimulusSender` tries to start new instances, then resume matching bookmarks.
2. If no bookmark matches, the stimulus is enqueued in the **bookmark queue**.
3. `TriggerBookmarkQueueRecurringTask` periodically signals the queue worker, so bookmarks created slightly later still pick up early stimuli.

So early callbacks are not lost — but there's a small delay. For strict ordering, have the caller retry or use correlation-targeted resume.

## Long-running instance lifecycle

- Statuses: `WorkflowStatus` is `Running`/`Finished`; the finer state lives in `WorkflowSubStatus`: `Pending`, `Executing`, `Suspended`, `Finished`, `Cancelled`, `Faulted`, `Interrupted`. A long-running instance oscillates `Executing` ↔ `Suspended` until it reaches `Finished`/`Cancelled`/`Faulted`.
- Each suspend commits state. On resume, the instance is locked (distributed lock) to avoid duplicate execution.
- Variables persist across bursts (those marked persisted).
- Activity execution logs accumulate — useful for debugging but watch retention.

## Designing for long-running

- **Idempotency**: activities may re-run after a crash mid-burst. Make side-effecting activities idempotent or guard with state.
- **Timeouts**: always pair an event-wait with a `Delay` via `Fork(WaitAny)` so instances don't hang forever.
- **Correlation**: always set a correlation ID for resumable instances; you'll need it for ops/debugging.
- **Retention**: completed instances accumulate; configure retention (chapter 12).
- **Versioning**: changing the workflow tree while instances are suspended can break resume. Use alterations or version carefully.

## Human-in-the-loop: User Tasks

Elsa has a User Tasks feature for human approvals:

```csharp
new UserTask
{
    // ...
}
```

Integrates with Elsa Studio's task inbox and a REST API. See [User Tasks](https://docs.elsaworkflows.io/guides/running-workflows/user-tasks.md).

## Next

Continue with [08 — Persistence](08-persistence.md).
