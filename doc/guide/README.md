# Elsa Workflows 3.10 — Beginner's Guide (Code-First)

> For experienced ASP.NET developers who are new to Elsa Workflows and intend to use Elsa as the core engine of the product(s) they build. This guide focuses on **how Elsa itself works** — hosting it, authoring workflows in code, running and resuming them, persisting state, and extending it — rather than on the visual designer.

**Target version:** Elsa Workflows **3.10.x** (this repo uses `3.10.2`).
**Audience:** you already know ASP.NET Core, DI, EF Core, async/await, and distributed systems basics. You do not need prior workflow-engine experience.
**Companion code:** the `src/Stepwise.Server` project in this repository is a minimal, runnable Elsa host. Every example is either taken from it or is a small, self-contained extension of it.

---

## How to read this guide

Read the chapters in order the first time. Each chapter builds on the previous one. After that, use the [cheat sheet](appendix-cheat-sheet.md) and the cross-references.

| # | Chapter | What you will learn |
|---|---------|---------------------|
| 01 | [What is Elsa?](01-what-is-elsa.md) | Mental model, core concepts, when to use it |
| 02 | [Hosting Elsa in ASP.NET Core](02-hosting-in-aspnetcore.md) | Packages, `AddElsa`, `UseWorkflows`, Program.cs |
| 03 | [Authoring workflows in code](03-authoring-workflows.md) | `WorkflowBase`, `IWorkflowBuilder`, activities, variables, inputs/outputs |
| 04 | [The execution model](04-execution-model.md) | Runner vs runtime vs dispatcher, bursts, bookmarks/triggers/stimuli |
| 05 | [Variables, inputs & outputs](05-variables-inputs-outputs.md) | Data flow inside a workflow |
| 06 | [Control flow](06-control-flow.md) | Sequence, Flowchart, Fork/Join, Decision, loops |
| 07 | [Long-running & event-driven workflows](07-long-running.md) | Bookmarks, triggers, correlation, resume |
| 08 | [Persistence](08-persistence.md) | EF Core setup, migrations, stores, in-memory vs durable |
| 09 | [Custom activities](09-custom-activities.md) | Writing your own activities and triggers |
| 10 | [Runtime APIs](10-runtime-apis.md) | Starting, resuming, cancelling, querying instances |
| 11 | [Testing & debugging](11-testing-debugging.md) | Unit testing activities/workflows, journal, incidents |
| 12 | [Operating in production](12-production.md) | Clustering, health checks, observability, tuning |
| A | [Cheat sheet](appendix-cheat-sheet.md) | Quick reference of types, APIs, common patterns |

## Conventions used in this guide

- **Code-first** examples use `WorkflowBase` and C# object initializers, exactly like the sample project. The designer is mentioned only where it affects runtime behavior (e.g. publishing a definition).
- `context` means an `ActivityExecutionContext` unless stated otherwise.
- `builder` means an `IWorkflowBuilder`.
- Types are referenced by their short name; full namespaces are `Elsa.Workflows`, `Elsa.Workflows.Activities`, `Elsa.Http`, etc.
- All snippets target .NET 11 / C# 14 style implicit usings where reasonable, matching the sample project.

## What this guide is NOT

- It is not a reference manual for every activity. Use the official [Activity Reference](https://docs.elsaworkflows.io/activities/activity-reference.md).
- It is not a Studio tutorial. Studio is covered only as far as it interacts with code-first workflows (publishing, inspecting instances).
- It does not cover every integration package (MassTransit, email, SQL, …). Each has its own doc page; this guide teaches the patterns that apply to all of them.

## Source of truth

When this guide and the official docs disagree, **the official docs and the Elsa source win**. This guide is a curated, opinionated path through them for a code-first ASP.NET developer. Key official references:

- [Concepts](https://docs.elsaworkflows.io/getting-started/concepts.md)
- [Architecture](https://docs.elsaworkflows.io/guides/architecture.md)
- [Execution Model](https://docs.elsaworkflows.io/guides/architecture/execution-model.md)
- [Hello World](https://docs.elsaworkflows.io/getting-started/hello-world.md)
- [Hosting Elsa in an existing app](https://docs.elsaworkflows.io/guides/onboarding/hosting-elsa-in-existing-app.md)
- [Custom activities](https://docs.elsaworkflows.io/extensibility/custom-activities.md)
- [Persistence](https://docs.elsaworkflows.io/guides/persistence.md)
