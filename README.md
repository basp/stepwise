# Stepwise

> A curated, code-first beginner's guide to **Elsa Workflows 3.10**, with a runnable sample host.

Stepwise exists to make Elsa Workflows approachable for experienced ASP.NET Core developers who are new to workflow engines and intend to use Elsa as the **core engine** of the products they build. It is documentation- and guidance-first: an opinionated, ordered path through Elsa's concepts, hosting, authoring, execution model, persistence, extensibility, runtime APIs, testing, and production operation — paired with a minimal, runnable host you can read, run, and extend.

This is **not** a reference manual for every activity, nor a Studio tutorial. It deliberately focuses on **how Elsa itself works** when you drive it from code, rather than through the visual designer. When this guide and the official Elsa docs disagree, the official docs and the Elsa source win.

## Goals

- **Lower the barrier to entry** for .NET developers adopting Elsa as a core engine, by explaining the mental model (workflows, activities, bookmarks, triggers, stimuli, outcomes, bursts) in plain terms.
- **Teach code-first authoring** using `WorkflowBase` and `IWorkflowBuilder`, exactly as you would ship in a real product, version-controlled alongside your application code.
- **Explain the execution model** — runner vs. runtime vs. dispatcher, bursts, bookmarks/triggers/stimuli, correlation — so you can reason about side effects, resumption, and transaction boundaries.
- **Cover the full lifecycle**: hosting in ASP.NET Core, variables/inputs/outputs, control flow, long-running and event-driven workflows, persistence (EF Core), custom activities, runtime APIs, testing/debugging, and production operation.
- **Provide a runnable companion** (`src/Stepwise.Server`) so every concept can be tried locally instead of just read about.
- **Stay version-pinned and honest**: target Elsa Workflows `3.10.x` (the sample uses `3.10.2`) on .NET 11, with explicit notes where behavior depends on a specific version.

## Who it's for

- You already know ASP.NET Core, dependency injection, EF Core, async/await, and distributed-systems basics.
- You do **not** need prior workflow-engine experience.
- You want workflows defined in code (version-controlled) but still inspectable at runtime.

## Who it's not for

- If you only need request/response with no state across requests, use controllers — Elsa is the wrong tool.
- If you need high-throughput, stateless data transformation, use pipelines/streams.
- If you need a strict BPMN-standards engine, Elsa supports BPMN import/export but is not a pure BPMN engine.

## What's inside

```
stepwise/
├── doc/
│   ├── README.md                 # Documentation index
│   └── guide/
│       ├── README.md             # Guide index & reading order
│       ├── 01-what-is-elsa.md
│       ├── 02-hosting-in-aspnetcore.md
│       ├── 03-authoring-workflows.md
│       ├── 04-execution-model.md
│       ├── 05-variables-inputs-outputs.md
│       ├── 06-control-flow.md
│       ├── 07-long-running.md
│       ├── 08-persistence.md
│       ├── 09-custom-activities.md
│       ├── 10-runtime-apis.md
│       ├── 11-testing-debugging.md
│       ├── 12-production.md
│       └── appendix-cheat-sheet.md
├── src/Stepwise.Server/          # Minimal, runnable Elsa host (companion code)
└── stepwise.slnx
```

### Reading order

Read the chapters in order the first time; each builds on the previous one. After that, use the [cheat sheet](doc/guide/appendix-cheat-sheet.md) and cross-references.

| # | Chapter | What you will learn |
|---|---------|---------------------|
| 01 | [What is Elsa?](doc/guide/01-what-is-elsa.md) | Mental model, core concepts, when to use it |
| 02 | [Hosting Elsa in ASP.NET Core](doc/guide/02-hosting-in-aspnetcore.md) | Packages, `AddElsa`, `UseWorkflows`, `Program.cs` |
| 03 | [Authoring workflows in code](doc/guide/03-authoring-workflows.md) | `WorkflowBase`, `IWorkflowBuilder`, activities, variables, inputs/outputs |
| 04 | [The execution model](doc/guide/04-execution-model.md) | Runner vs. runtime vs. dispatcher, bursts, bookmarks/triggers/stimuli |
| 05 | [Variables, inputs & outputs](doc/guide/05-variables-inputs-outputs.md) | Data flow inside a workflow |
| 06 | [Control flow](doc/guide/06-control-flow.md) | Sequence, Flowchart, Fork/Join, Decision, loops |
| 07 | [Long-running & event-driven workflows](doc/guide/07-long-running.md) | Bookmarks, triggers, correlation, resume |
| 08 | [Persistence](doc/guide/08-persistence.md) | EF Core setup, migrations, stores, in-memory vs. durable |
| 09 | [Custom activities](doc/guide/09-custom-activities.md) | Writing your own activities and triggers |
| 10 | [Runtime APIs](doc/guide/10-runtime-apis.md) | Starting, resuming, cancelling, querying instances |
| 11 | [Testing & debugging](doc/guide/11-testing-debugging.md) | Unit testing activities/workflows, journal, incidents |
| 12 | [Operating in production](doc/guide/12-production.md) | Clustering, health checks, observability, tuning |
| A | [Cheat sheet](doc/guide/appendix-cheat-sheet.md) | Quick reference of types, APIs, common patterns |

### Conventions

- **Code-first** examples use `WorkflowBase` and C# object initializers, exactly like the sample project.
- `context` means an `ActivityExecutionContext` unless stated otherwise.
- `builder` means an `IWorkflowBuilder`.
- Types are referenced by their short name; full namespaces are `Elsa.Workflows`, `Elsa.Workflows.Activities`, `Elsa.Http`, etc.
- All snippets target .NET 11 / C# 14 style implicit usings where reasonable, matching the sample project.

### The sample host

`src/Stepwise.Server` is a minimal, runnable Elsa host. Every guide example is either taken from it or is a small, self-contained extension of it. It registers two workflows (`EchoWorkflow`, `Test`) over HTTP endpoints and demonstrates correlation, forking, delays, and resumption.

## Getting started

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) (the sample targets `net11.0`)
- (Optional) A database if you want durable persistence — the sample runs with in-memory defaults.

### Run the sample

```sh
cd src/Stepwise.Server
dotnet run
```

Then exercise the endpoints (base URL `http://localhost:5268`, workflow path `/workflows`):

```sh
# Start an echo instance
curl http://localhost:5268/workflows/echo

# Start a long-running instance (returns a correlation id)
curl http://localhost:5268/workflows/test

# Resume it via the finish branch using the correlation id
curl "http://localhost:5268/workflows/finish?correlationId=<id>"
```

### Read the guide

Start at [doc/guide/README.md](doc/guide/README.md), or jump straight to [01 — What is Elsa?](doc/guide/01-what-is-elsa.md).

## Key concepts at a glance

| Concept | One-liner |
|---------|-----------|
| **Workflow** | A tree/composition of activities forming a process (`Workflow` definition, `WorkflowInstance` occurrence). |
| **Activity** | The atomic unit of work; has inputs, outputs, and named outcomes (`Done`, `True`/`False`, …). |
| **Definition vs. instance** | Blueprint (code class/JSON, versioned) vs. one execution with its own state. |
| **Bookmark** | A pause point; state is persisted until a matching stimulus arrives. |
| **Trigger** | An activity (`CanStartWorkflow = true`) that can start new instances (HTTP hit, timer). |
| **Stimulus** | An external event matched against triggers (start) and bookmarks (resume). |
| **Outcome** | Named result connecting an activity to the next. |
| **Variable** | Workflow-level named storage; outputs can auto-bind to variables. |
| **Incident** | A recorded fault during execution; inspectable, with retry/fail strategies. |
| **Burst** | The period the runner actively executes activities; a bookmark creates multiple bursts. |
| **Correlation ID** | Flexible id linking instances and external entities for targeted resumes. |
| **Alteration** | A change applied to a running instance (state modification, scheduling). |

## Execution surfaces

| Surface | Interface | Semantics | When to use |
|---------|-----------|-----------|-------------|
| Direct execution | `IWorkflowRunner` | In-process, synchronous, no queue, no persistence by default | Tests, one-shot scripts, custom host code |
| Runtime client | `IWorkflowRuntime` | Higher-level API over persisted instances: start, run, resume, cancel | Most application code managing instances |
| Background dispatch | `IWorkflowDispatcher` | Queues work; background workers execute; supports distributed nodes | Production server-side execution, child workflows, triggers |

Plus stimulus delivery (`IStimulusSender`/`IStimulusDispatcher`) for external events (HTTP, timers, messages). Rule of thumb: **author and test with `IWorkflowRunner`; operate in production with `IWorkflowRuntime` + `IWorkflowDispatcher`.**

## Contributing

This repository is primarily a documentation/guidance project. Contributions that improve clarity, fix inaccuracies against Elsa 3.10, or extend the companion sample are welcome. When the guide and official Elsa docs disagree, prefer the official docs and Elsa source. See the [official Elsa docs](https://docs.elsaworkflows.io/) and [Elsa on GitHub](https://github.com/elsa-workflows/elsa-core).

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 Bas Pennings.