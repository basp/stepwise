# Design

## Context

See `proposal.md` for motivation and `specs/documentation/persistence-guide/spec.md` for the required behavior.

The current chapter mixes older per-feature persistence guidance with newer runtime concepts. It configures only workflow runtime persistence, treats SQL Server as a special case without an Elsa provider package, and describes a single `ElsaDbContext` that does not match the 3.10.2 EF Core model.

The repository targets Elsa 3.10.2. Commit-pinned source and the installed assemblies confirm separate management and runtime persistence features, provider-specific helpers, and separate EF Core contexts. They also establish four boundaries the guide must state accurately:

- Provider helpers extend the EF Core persistence feature inside `UseEntityFrameworkCore(...)`, not the management or runtime feature directly.
- `RunMigrations` defaults to `true`; production examples must explicitly disable it.
- The two contexts have separate migration streams but share `__EFMigrationsHistory` by default when they target the same database; schema-capable providers place it in the `Elsa` schema.
- Elsa 3.10.2 MySQL only supports .NET 8 and .NET 9 assets. A .NET 11 build selects an unsupported fallback.

## Goals / Non-Goals

**Goals:**

- Make the persistence chapter accurate for Elsa 3.10.2.
- Give readers one complete durable path and a clear provider-swap path.
- Explain management/runtime boundaries before showing migrations or production advice.
- Keep all persistence references in the guide consistent.
- Validate examples without changing the sample host.

**Non-Goals:**

- Implementing durable persistence in `src/Stepwise.Server`.
- Adding a database, container, or integration-test project to the repository.
- Teaching custom `DbContext` design, advanced indexing, or provider-specific performance tuning.
- Addressing deferred revisions #2-#5.

## Decisions

### 1. Teach two persistence boundaries

The chapter will introduce management and runtime persistence before presenting code.

```text
+-----------------------------+       +-----------------------------+
| Workflow Management         |       | Workflow Runtime            |
|-----------------------------|       |-----------------------------|
| Workflow definitions         |       | Bookmark queue + dead letters|
| Definition registry data     |       | Bookmarks                   |
| Workflow instances           |       | Triggers                    |
+-------------+---------------+       | Execution logs               |
              |                       | Key-value outbox/coordination|
              |                       +-------------+---------------+
              +------------------+--------------------------+
                                 |
                      same database or separate databases
                      migrate each context independently
```

The activity execution queue is an in-process burst mechanism, not a durable store. `RuntimeElsaDbContext` retains a legacy inbox table for schema compatibility, but EF Core 3.10.2 does not register an active durable inbox store. This prevents readers from assuming that runtime persistence also persists definitions or instances, or that every runtime-looking queue is durable.

### 2. Use provider-specific Elsa helpers as the primary path

The minimal example will use the helper supplied by the selected Elsa provider package:

| Provider | Elsa package | Helper | Supported package targets |
|---|---|---|---|
| SQLite | `Elsa.Persistence.EFCore.Sqlite` | `UseSqlite` | .NET 8, 9, 10 |
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `UseSqlServer` | .NET 8, 9, 10 |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `UsePostgreSql` | .NET 8, 9, 10 |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `UseMySql` | .NET 8, 9 only |

The helpers live in `Elsa.Persistence.EFCore.Extensions` and are invoked on the persistence feature passed to `UseEntityFrameworkCore(...)`:

```csharp
elsa.UseWorkflowManagement(management =>
    management.UseEntityFrameworkCore(ef => ef.UseSqlite(connectionString)));

elsa.UseWorkflowRuntime(runtime =>
    runtime.UseEntityFrameworkCore(ef => ef.UseSqlite(connectionString)));
```

These helpers configure the migrations assembly, history table, schema, and provider-specific model behavior. Manual `DbContextOptionsBuilder` configuration will not be the recommended beginner path because it bypasses that setup.

### 3. Use SQLite for the complete copy-paste example

SQLite will be the initial durable example because it requires no separate database server and lets readers verify persistence locally. A compact provider-swap table will cover SQL Server, PostgreSQL, and MySQL while showing that the API shape is the same.

The guide will call out SQLite's concurrency and operational limitations and direct clustered or higher-throughput production deployments to SQL Server, PostgreSQL, or another supported provider. MySQL will retain its package/helper entry, but the guide will explicitly state that .NET 11 uses an unsupported `net9.0` asset fallback and that the restore emits an EF Core version-constraint warning. MySQL must not be presented as operationally equivalent on the repository's target framework.

### 4. Separate development and production migration guidance

The provider helpers select each context's migration stream. `RunMigrations` defaults to `true`, so the development example may show it explicitly and label it development-only. The production example will set `ef.RunMigrations = false` on both features and apply reviewed migrations through the deployment pipeline. The startup project must reference `Microsoft.EntityFrameworkCore.Design` 10.0.9 with `PrivateAssets="all"` so `dotnet ef` can discover both contexts.

Both contexts may target the same physical database. They have context-specific migration sets filtered by `[DbContext(...)]`, but by default share the `__EFMigrationsHistory` table, located in the `Elsa` schema on schema-capable providers. Literal table separation requires different `ElsaDbContextOptions.MigrationsHistoryTableName` values.

Production guidance will show explicit commands for both contexts:

```text
dotnet ef database update --project <startup-project> --context ManagementElsaDbContext
dotnet ef database update --project <startup-project> --context RuntimeElsaDbContext
```

The chapter will explain the shared-history-table nuance rather than claiming that co-located contexts automatically have separate physical history tables.

### 5. Keep the change documentation-only

The sample host remains unchanged. Snippet validation will use a throwaway project against Elsa 3.10.2. The restart/resume check will store a workflow definition, create a suspended instance and bookmark in SQLite, stop the host, start a new process, and resume the stored bookmark to completion.

Adding durable persistence to the sample is a separate concern because it introduces package, configuration, migration, and runtime behavior changes.

### 6. Limit consistency edits to persistence references

Chapter 02 and the cheat sheet will be updated only where they describe persistence packages, contexts, or host wiring. Unrelated API corrections remain in deferred revisions #2-#5.

### 7. Preserve alternative providers with accurate coverage

MongoDB and Dapper will be presented as alternatives for the core management/runtime store set, with their own migration or index responsibilities. Both leave definition-registry generation coordination on the process-local in-memory store in 3.10.2, which the guide must disclose for multi-node readers. Elasticsearch will be presented only as a replacement for `IWorkflowInstanceStore` and `IWorkflowExecutionLogStore`; definitions, bookmarks, queues, activity execution records, key-values, and other stores require separate configuration.

## Risks / Trade-offs

- **Official documentation may describe an earlier Elsa release** -> Pin examples to the installed 3.10.2 assemblies and commit-pinned source, and record the validated package/helper matrix in the chapter.
- **Readers may copy automatic migrations into production** -> State that `RunMigrations` defaults to `true`, show `false` explicitly in production guidance, and repeat the controlled migration path in the checklist.
- **A shared history table can hide the two-context model** -> Explain separate migration streams and the shared default `__EFMigrationsHistory` table, including its provider-dependent schema qualification; require both context commands even when connection strings match.
- **MySQL appears API-compatible but lacks a supported .NET 11 target** -> Document the unsupported fallback and restore warning; do not recommend it as a supported .NET 11 production swap.
- **Runtime terminology can imply durability that does not exist** -> Map bookmark queues, dead letters, and key-value outbox records explicitly; exclude the in-process activity queue and deprecated inbox from durable-store claims.
- **Provider packages have different operational characteristics** -> Keep the common configuration shape, but avoid claiming SQLite, SQL Server, PostgreSQL, and MySQL are operationally interchangeable.
- **Documentation snippets can drift again** -> Require compilation and a restart/resume smoke check as part of this revision, and recommend a reusable documentation-validation follow-up.
- **Scope can expand into the rest of the guide review** -> Keep revisions #2-#5 in the proposal backlog and reject unrelated edits during this change.

## Migration Plan

1. Rewrite chapter 08 around the two persistence boundaries and verified queue/coordination mapping.
2. Replace the package matrix and minimal EF Core configuration with verified 3.10.2 packages, helper placement, and target-framework boundaries.
3. Replace the migration section with the two-context lifecycle, shared-history-table nuance, and production guidance that disables automatic migrations.
4. Correct restart semantics, the decision matrix, existing-app guidance, and alternative-provider boundaries.
5. Update persistence references in chapter 02 and the cheat sheet.
6. Compile each documented provider configuration against Elsa 3.10.2 and record the MySQL limitation.
7. Run an isolated durable restart/resume check for a bookmarked workflow.
8. Review the diff for stale persistence terms and unrelated changes.

Rollback is limited to reverting documentation changes. No application data, schema, package reference, or runtime configuration is migrated by this change.

## Open Questions

None. Adding persistence to the sample host or introducing a permanent documentation-test project can be proposed separately if desired.
