# Design

## Context

See `proposal.md` for motivation and `specs/documentation/persistence-guide/spec.md` for the required behavior.

The current chapter mixes older per-feature persistence guidance with newer runtime concepts. It configures only workflow runtime persistence, treats SQL Server as a special case without an Elsa provider package, and describes a single `ElsaDbContext` that does not match the 3.10.2 EF Core model.

The repository targets Elsa 3.10.2. The installed assemblies and the matching Elsa source expose separate management and runtime persistence features, provider-specific helpers, and separate EF Core contexts.

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
| Workflow definitions         |       | Bookmarks                   |
| Definition registry data     |       | Triggers                    |
| Workflow instances           |       | Inbox / bookmark queues     |
+-------------+---------------+       | Execution logs              |
              |                               | Key-value coordination      |
              |                               +-------------+---------------+
              |                                             |
              +------------------+--------------------------+
                                 |
                    same database or separate databases
                    migrate each context independently
```

This prevents readers from assuming that configuring runtime persistence also persists definitions and instances.

### 2. Use provider-specific Elsa helpers as the primary path

The minimal example will use the helper supplied by the selected Elsa provider package:

| Provider | Elsa package | Helper |
|---|---|---|
| SQLite | `Elsa.Persistence.EFCore.Sqlite` | `UseSqlite` |
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `UseSqlServer` |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `UsePostgreSql` |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `UseMySql` |

These helpers configure the migrations assembly and provider-specific model behavior. Manual `DbContextOptionsBuilder` configuration will not be the recommended beginner path because it bypasses that setup.

### 3. Use SQLite for the complete copy-paste example

SQLite will be the initial durable example because it requires no separate database server and lets readers verify persistence locally. A compact provider-swap table will cover SQL Server, PostgreSQL, and MySQL while showing that the persistence shape is the same across providers.

The guide will call out SQLite's concurrency and operational limitations and direct clustered or higher-throughput production deployments to SQL Server, PostgreSQL, or another supported provider.

### 4. Separate development and production migration guidance

The development example may show `RunMigrations = true` on both persistence features, clearly labeled as development-only.

Production guidance will show explicit commands for both contexts:

```text
dotnet ef database update --context ManagementElsaDbContext
dotnet ef database update --context RuntimeElsaDbContext
```

The chapter will explain that both contexts may target the same physical database while retaining separate migration histories.

### 5. Keep the change documentation-only

The sample host remains unchanged. Snippet validation will use a throwaway project or an equivalent isolated check against Elsa 3.10.2. Adding durable persistence to the sample is a separate concern because it introduces package, configuration, migration, and runtime behavior changes.

### 6. Limit consistency edits to persistence references

Chapter 02 and the cheat sheet will be updated only where they describe persistence packages, contexts, or host wiring. Unrelated API corrections remain in deferred revisions #2-#5.

## Risks / Trade-offs

- **Official documentation may describe an earlier Elsa release** -> Pin examples to the installed 3.10.2 assemblies and matching source, and record the validated package/helper matrix in the chapter.
- **Readers may copy `RunMigrations = true` into production** -> Place the development-only warning next to the setting and repeat the production migration path in the checklist.
- **A single physical database can hide the two-context model** -> Use the boundary diagram and require both migration commands even when connection strings are identical.
- **Provider packages have different operational characteristics** -> Keep the common configuration shape, but avoid claiming that SQLite, SQL Server, PostgreSQL, and MySQL are operationally interchangeable.
- **Documentation snippets can drift again** -> Require compilation and a restart/resume smoke check as part of this revision, and recommend a reusable documentation-validation follow-up.
- **Scope can expand into the rest of the guide review** -> Keep revisions #2-#5 in the proposal backlog and reject unrelated edits during this change.

## Migration Plan

1. Rewrite chapter 08 around the two persistence boundaries.
2. Replace the package matrix and minimal EF Core configuration with verified 3.10.2 packages and helpers.
3. Replace the migration section with the two-context lifecycle and production guidance.
4. Correct restart semantics, the decision matrix, existing-app guidance, and alternative-provider boundaries.
5. Update persistence references in chapter 02 and the cheat sheet.
6. Compile each documented provider configuration against Elsa 3.10.2.
7. Run an isolated durable restart/resume check for a bookmarked workflow.
8. Review the diff for stale persistence terms and unrelated changes.

Rollback is limited to reverting documentation changes. No application data, schema, package reference, or runtime configuration is migrated by this change.

## Open Questions

None. Adding persistence to the sample host or introducing a permanent documentation-test project can be proposed separately if desired.
