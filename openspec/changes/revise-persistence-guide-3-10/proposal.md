# Proposal

## Why

The persistence chapter teaches obsolete Elsa 3.x persistence APIs and an incomplete EF Core setup. A reader can follow it and still lose definitions, instances, or bookmarks after a restart, so the guide is not yet safe as a copy-paste path for Elsa 3.10.2.

## What Changes

- Rewrite `doc/guide/08-persistence.md` around the Elsa 3.10.2 management and runtime persistence boundaries.
- Document the actual EF Core provider packages and helpers for SQL Server, PostgreSQL, SQLite, and MySQL.
- Show durable configuration for both `UseWorkflowManagement` and `UseWorkflowRuntime`.
- Replace the singular `ElsaDbContext` migration story with `ManagementElsaDbContext` and `RuntimeElsaDbContext`, including separate context-specific migration streams, their shared default history table when co-located, and controlled production migration commands.
- Correct the in-memory/durable decision matrix and clarify which records survive a restart.
- Preserve alternative providers while stating their real coverage boundaries, especially Elasticsearch and process-local definition-registry coordination in MongoDB and Dapper.
- Document verified provider limitations that affect copy-paste guidance: MySQL 3.10.2 has no supported .NET 11 target, the active durable runtime queue is the bookmark queue plus dead-letter store, and the legacy inbox table is not an active EF Core inbox store.
- Update persistence references in `doc/guide/02-hosting-in-aspnetcore.md` and `doc/guide/appendix-cheat-sheet.md` for consistency.
- Validate revised snippets against Elsa 3.10.2, including a restart/resume check for a bookmarked workflow.
- Record revisions #2-#5 as deferred backlog; they are not part of this change.

## Capabilities

### New Capabilities

- `documentation/persistence-guide`: Accurate, version-pinned guidance for selecting, configuring, migrating, and operating Elsa persistence in a code-first ASP.NET Core host.

### Modified Capabilities

None.

## Impact

- Documentation: `doc/guide/08-persistence.md`, persistence notes in `doc/guide/02-hosting-in-aspnetcore.md`, and persistence entries in `doc/guide/appendix-cheat-sheet.md`.
- Validation: revised snippets are checked against Elsa 3.10.2 packages and APIs, including provider target-framework boundaries.
- No production code, sample-host behavior, database schema, or runtime configuration is changed by this revision.

## Deferred Backlog

- **#2 - Custom activities:** attributes, service resolution, bookmark stimuli, trigger base classes, and completion behavior.
- **#3 - Control flow:** `FlowDecision`, `Switch`, `ParallelForEach`, `ExecuteWorkflow`, and child-workflow semantics.
- **#4 - Runtime APIs:** dispatch requests, start/resume APIs, filters, stimulus metadata, and instance querying.
- **#5 - Guide consistency:** timer inputs, workflow inputs/outputs, incidents, API feature wiring, correlation uniqueness, and timeout/early-callback guidance.
