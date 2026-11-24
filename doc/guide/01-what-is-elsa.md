# 01 — What is Elsa Workflows?

Elsa is an **embeddable, code-first workflow engine for .NET**. You host it inside your own ASP.NET Core (or console) process, define workflows as C# objects or JSON, and Elsa takes care of scheduling activities, persisting state, pausing for external events, and resuming later. Think of it as a durable, stateful orchestrator that lives in-process with your app.

## Why a workflow engine at all?

Most business processes are not one request/response. They span time, systems, and people:

- An order that waits for payment, then for fulfillment, then for delivery confirmation.
- An approval that waits for a manager's click, with a 48h timeout and an escalation.
- A data pipeline that fans out to 5 services and joins when all finish.
- A retry loop with backoff around a flaky third-party API.

You *can* build all of this with queues, state machines, and a database. Elsa gives you a higher-level vocabulary (activities, bookmarks, triggers, outcomes) and a runtime that handles the plumbing: persistence, scheduling, correlation, distributed locking, resumption.

## The three execution surfaces

Elsa exposes three ways to run a workflow. Understand these first — everything else builds on them.

| Surface | Interface | Semantics | When to use |
|---------|-----------|-----------|-------------|
| **Direct execution** | `IWorkflowRunner` | Runs in-process, synchronously, no queue, no persistence by default | Tests, simple one-shot scripts, custom host code |
| **Runtime client** | `IWorkflowRuntime` | Higher-level API over persisted instances: start, run, resume, cancel | Most application code that manages instances |
| **Background dispatch** | `IWorkflowDispatcher` | Queues work; background workers execute it; supports distributed nodes | Production server-side execution, child workflows, triggers |

Plus a fourth, orthogonal one: **stimulus delivery** (`IStimulusSender`/`IStimulusDispatcher`) — how external events start new instances or resume waiting ones (HTTP, timers, messages).

Rule of thumb: **author and test with `IWorkflowRunner`; operate in production with `IWorkflowRuntime` + `IWorkflowDispatcher`.**

## Core concepts (learn these words)

### Workflow
A sequence/composition of **activities** forming a process. Represented by `Workflow` (definition) and `WorkflowInstance` (a running/persisted occurrence). A workflow has a `Root` activity of type `IActivity`.

### Activity
The atomic unit of work. A class implementing `IActivity` (or subclassing `Activity`, `CodeActivity<T>`, etc.). Activities have inputs, outputs, and **outcomes** (named ports: `Done`, `True`/`False`, …). The engine schedules activities based on outcomes and structure.

### Workflow definition vs. instance
- **Definition** = the blueprint (code class or JSON). Versioned.
- **Instance** = one execution of a definition at a point in time, with its own variables, bookmarks, state. Persisted.

### Bookmark
A **pause point** inside an instance. Created by blocking activities (`Event`, `Delay`, `HttpEndpoint` waiting, …). When a bookmark is created, state is persisted and execution suspends until a matching **stimulus** arrives. Think "save point".

### Trigger
An activity with `Kind = Trigger` that can **start** new instances automatically. Marked `CanStartWorkflow = true`. Triggers are indexed when a definition is published. Examples: `HttpEndpoint` (URL hit), `Timer` (cron).

### Stimulus
An external event Elsa tries to match against triggers (start new instance) and bookmarks (resume existing instance). One stimulus can do both.

### Outcome
Named result of an activity. Activities connect to next activities via outcomes. `Decision` has `True`/`False`. Outcomes eliminate the need for explicit if/else activities in many cases.

### Variable
Workflow-level (or scoped) named storage. Set via `SetVariable`, read in expressions. Activity outputs can auto-bind to variables.

### Input / Output
- **Activity input**: a property on an activity (e.g. `WriteLine.Text`).
- **Activity output**: data produced (e.g. HTTP response body).
- **Workflow input/output**: data passed in when starting / returned when completing.

### Incident
An error recorded during execution (an activity faulted). Incidents are persisted and inspectable; strategies control retry/fail behavior.

### Burst of execution
The period the runner actively executes activities. A workflow that runs start-to-finish is one burst. A workflow that hits a bookmark has multiple bursts (resume on stimulus). Important for reasoning about side effects and transaction boundaries.

### Correlation ID
A flexible identifier linking related instances and external entities (orders, customers). Used to target resumes ("resume the instance for order 42").

### Alteration
A change applied to a running instance (modify state, schedule activities). Used for migrations/ops.

## When to use Elsa (and when not)

**Good fit:**
- Long-running, event-driven, stateful processes.
- Processes that need auditability, resumption after restart, human-in-the-loop.
- You want workflows defined by code (version-controlled) but still inspectable.
- Multi-tenant SaaS where each tenant has its own process variants.

**Poor fit:**
- Pure request/response with no state across requests — just use controllers.
- High-throughput, stateless data transformation — use pipelines/streams.
- You need a graphical BPMN engine with strict standards compliance — Elsa supports BPMN import/export but is not a pure BPMN engine.

## Mental model in one paragraph

You define a **workflow** as a tree of **activities**. You **publish** it (register it with the engine). When a **trigger** fires (HTTP hit, timer), Elsa creates an **instance**, runs a **burst** of activities, and either **completes** or creates a **bookmark** and **persists** state. Later, an external event becomes a **stimulus**, Elsa matches it to a bookmark, **resumes** the instance from that point, and runs another burst. Variables carry data across the whole lifetime. Everything is persisted (if you configure persistence), so instances survive restarts.

## Next

Continue with [02 — Hosting Elsa in ASP.NET Core](02-hosting-in-aspnetcore.md).
