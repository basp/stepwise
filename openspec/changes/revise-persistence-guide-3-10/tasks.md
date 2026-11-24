# Tasks

## 1. Verify the Elsa 3.10.2 persistence reference

- [x] 1.1 Confirm the EF Core provider package, helper, namespace, and target-framework matrix for SQL Server, PostgreSQL, SQLite, and MySQL against Elsa 3.10.2 packages or matching source, and verify every documented name exists and each helper compiles in its supported configuration.
- [x] 1.2 Confirm the management/runtime store mapping, bookmark queue and dead-letter coverage, key-value outbox role, deprecated inbox boundary, and the `ManagementElsaDbContext` / `RuntimeElsaDbContext` migration-stream split against the installed assemblies, including their shared default history table when co-located.
- [x] 1.3 Prepare an isolated snippet-validation project outside the repository and verify it restores Elsa 3.10.2 plus the four EF Core provider packages without modifying `src/Stepwise.Server`.

## 2. Rewrite the persistence chapter

- [x] 2.1 Replace the provider table in `doc/guide/08-persistence.md` with the verified package/helper/target-framework matrix and accurate alternative-provider boundaries, and verify Elasticsearch is described only as a replacement for workflow-instance and workflow-execution-log stores.
- [x] 2.2 Replace the minimal EF Core example with complete `UseWorkflowManagement` and `UseWorkflowRuntime` configuration using SQLite, explicitly disable automatic migrations for the production path, and verify the snippet compiles against Elsa 3.10.2.
- [x] 2.3 Add SQL Server, PostgreSQL, and MySQL provider-swap guidance using `UseSqlServer`, `UsePostgreSql`, and `UseMySql` inside each `UseEntityFrameworkCore(...)` callback, document MySQL's unsupported .NET 11 fallback, and verify each documented configuration compiles with its matching package.
- [x] 2.4 Add the management/runtime boundary diagram and store mapping, including bookmark queues, dead letters, and key-value outbox records, and verify the chapter excludes the in-process activity queue and deprecated inbox from durable-store claims and states that runtime-only persistence does not make definitions or instances durable.
- [x] 2.5 Correct restart semantics and the in-memory/durable decision matrix, and verify the chapter distinguishes code-first definitions recreated from source from stored definitions and runtime records that are lost with in-memory stores.
- [x] 2.6 Replace the migration section with separate `ManagementElsaDbContext` and `RuntimeElsaDbContext` migration-stream guidance, explain their shared default `__EFMigrationsHistory` table when co-located, state that `RunMigrations` defaults to `true`, and verify production guidance sets it to `false` while both context names appear in explicit commands.
- [x] 2.7 Rewrite the existing-application, shared-database, backup, and provider-boundary guidance, and verify the chapter no longer recommends reusing the domain `DbContext`, treating Elsa as a single context, equating key-values with distributed locks, or presenting MySQL as a supported .NET 11 swap.

## 3. Align related guide files

- [x] 3.1 Update the persistence notes in `doc/guide/02-hosting-in-aspnetcore.md`, and verify they describe separate Elsa contexts, independent migration streams with a shared default history table when co-located, and safe coexistence with a domain `DbContext`.
- [x] 3.2 Update the package list and host-wiring example in `doc/guide/appendix-cheat-sheet.md`, and verify the obsolete `UseEntityFrameworkPersistence(...)` placeholder is removed and the provider package names and helper placement match the revised chapter.

## 4. Validate the completed revision

- [x] 4.1 Compile every revised C# persistence snippet against Elsa 3.10.2 and verify there are no missing package references, obsolete context names, direct feature-level helper calls, or unsupported helper usage, while recording the MySQL target-framework and restore warnings.
- [x] 4.2 Run an isolated SQLite restart/resume smoke check with both persistence boundaries configured, and verify a stored definition, suspended instance, and its bookmark survive restart and the workflow resumes to completion.
- [x] 4.3 Scan the revised guide for stale persistence claims, including singular `ElsaDbContext` guidance, `UseEntityFrameworkPersistence`, the claim that SQL Server has no Elsa-specific package, direct `management.UseSqlite(...)`-style calls, durable inbox/activity-queue claims, and unsupported MySQL .NET 11 parity, and verify no active guidance contains them.
- [ ] 4.4 Review the final diff against `specs/documentation/persistence-guide/spec.md` and `design.md`, and verify all acceptance scenarios are covered while revisions #2-#5 remain deferred and unimplemented.

## Workflow follow-up

- After implementation and review are complete, archive the change with `/opsx-archive`.
- Verify the archived capability spec and deferred backlog remain traceable.
