# 06 — Control flow

Elsa provides both imperative (Sequence) and graph-based (Flowchart) control flow, plus concurrency primitives.

## Sequence

Run children top-to-bottom. The default and most common.

```csharp
new Sequence
{
    Activities = { a, b, c }   // a → b → c
}
```

Each child's `Done` outcome leads to the next. If a child creates a bookmark, the sequence suspends; on resume it continues with the next child.

## Flowchart

Explicit graph: activities as nodes, connections via outcomes. Use when you need arbitrary jumps, loops, or complex branching that's awkward in sequences.

```csharp
new Flowchart
{
    Activities = { start, check, doWork, finish },
    Connections =
    {
        new Connection(start, check),
        new Connection(check, doWork, "True"),
        new Connection(check, finish, "False"),
        new Connection(doWork, check),   // loop
    }
}
```

Outcomes become connection labels. `Flowchart` is the designer's underlying model; in code it's more verbose but more flexible.

## Branching

### `If` activity

```csharp
new If
{
    Condition = new(context => x > 0),
    Then = new WriteLine("positive"),
    Else = new WriteLine("non-positive"),
}
```

### Outcome-based branching (preferred)

Activities declare outcomes; you route via `Flowchart` connections or via `Switch`. `Decision` activity has `True`/`False` outcomes:

```csharp
new Decision { /* condition input */ }  // outcomes True/False
```

Using outcomes keeps the graph declarative and inspectable.

### `Switch`

```csharp
new Switch
{
    Expression = new(context => status),
    Cases =
    {
        new SwitchCase("Pending", pendingBranch),
        new SwitchCase("Done", doneBranch),
    },
    Default = defaultBranch,
}
```

## Concurrency

### `Parallel`

Runs children concurrently; completes when **all** complete.

```csharp
new Parallel
{
    Activities = { taskA, taskB, taskC }
}
```

### `Fork` / `Join`

`Fork` schedules multiple branches. `JoinMode` controls completion:

- `WaitAll` — completes when all branches complete (like Parallel).
- `WaitAny` — completes when the first branch completes; remaining branches are **cancelled**.

```csharp
new Fork
{
    JoinMode = ForkJoinMode.WaitAny,
    Branches =
    {
        new Sequence { Activities = { waitForFinish } },
        new Sequence { Activities = { new Delay(timeout), new WriteLine("Timeout.") } },
    },
}
```

This is the canonical "wait for event OR timeout" pattern (see sample `Test` workflow). Note: on `WaitAny`, the losing branch is cancelled — make sure its activities handle cancellation gracefully (e.g. don't leave side effects half-done).

### `ParallelForEach`

```csharp
new ParallelForEach<string>
{
    Items = new(context => items),
    Body = item => new WriteLine { Text = new(context => item) },
}
```

## Loops

### `While`

```csharp
new While
{
    Condition = new(context => i < n),
    Body = new Sequence { Activities = { /* ... */ } },
}
```

### `ForEach`

Sequential iteration (use `ParallelForEach` for concurrency).

### `DoWhile`, `ForEach` (list), `Loop`/`Break` — see activity reference.

## Error handling & compensation

- **Try/Catch/Finally**: `TryCatch` activity wraps a body with catch handlers and finally.
- **Compensation**: Elsa supports compensation (saga-style) via `Compensate`/`Compensation` activities for rollback scenarios. See patterns guide.
- **Incidents**: an unhandled fault becomes an incident on the instance; strategies control retry/suspend/fail (chapter 11).

## Sub-workflows

- `RunWorkflow` — runs a child workflow **inline** (same burst, shares execution context semantics; child completes before parent continues).
- `DispatchWorkflow` — dispatches a child workflow as a separate instance (async, queued).
- `RunWorkflow` with `WaitForCompletion = true` blocks the parent until the child finishes and can capture its output.

Choose based on coupling: tightly coupled steps → `RunWorkflow`; independent/long-running child → `DispatchWorkflow`.

## Composition tips

- Prefer `Sequence` + small activities over deep nesting. Deeply nested trees are hard to inspect.
- Extract reusable chunks into separate workflow definitions and call via `RunWorkflow`/`DispatchWorkflow`, or into custom composite activities.
- Keep branches short; push logic into custom activities rather than giant delegates.
- Use stable activity IDs when you need to target an activity (alterations, bookmarks).

## Next

Continue with [07 — Long-running & event-driven workflows](07-long-running.md).
