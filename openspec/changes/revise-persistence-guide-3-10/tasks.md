# Tasks

## 1. Verify the Elsa 3.10.2 persistence reference

- [ ] 1.1 Confirm the EF Core provider package and helper matrix for SQL Server, PostgreSQL, SQLite, and MySQL against Elsa 3.10.2 packages or matching source, and verify every documented name exists.
- [ ] 1.2 Confirm the management/runtime store mapping and the `ManagementElsaDbContext` / `RuntimeElsaDbContext` migration split against the installed assemblies, and verify the resulting notes cover definitions, instances, bookmarks, triggers, logs, queues, and key-value records.
- [ ] 1.3 Prepare an isolated snippet-validation project outside the repository and verify it restores Elsa 3.10.2 plus the four EF Core provider packages without modifying `src/Stepwise.Server`.

## 2. Rewrite the persistence chapter

- [ ] 2.1 Replace the provider table in `doc/guide/08-persistence.md` with the verified package/helper matrix and accurate alternative-provider boundaries, and verify Elasticsearch is not described as a full replacement for every store.
- [ ] 2.2 Replace the minimal EF Core example with complete `UseWorkflowManagement` and `UseWorkflowRuntime` configuration using SQLite, and verify the snippet compiles against Elsa 3.10.2.
- [ ] 2.3 Add SQL Server, PostgreSQL, and MySQL provider-swap guidance using `UseSqlServer`, `UsePostgreSql`, and `UseMySql`, and verify each documented configuration compiles with its matching package.
- [ ] 2.4 Add the management/runtime boundary diagram and store mapping, and verify the chapter explicitly states that runtime-only persistence does not make definitions or instances durable.
- [ ] 2.5 Correct restart semantics and the in-memory/durable decision matrix, and verify the chapter distinguishes code-first definitions recreated from source from stored definitions and runtime records that are lost with in-memory stores.
- [ ] 2.6 Replace the migration section with separate `ManagementElsaDbContext` and `RuntimeElsaDbContext` guidance, and verify both context names appear in production commands while automatic migrations are labeled development-only.
- [ ] 2.7 Rewrite the existing-application, shared-database, backup, and provider-boundary guidance, and verify the chapter no longer recommends reusing the domain `DbContext` or treating Elsa as a single context.

## 3. Align related guide files

- [ ] 3.1 Update the persistence notes in `doc/guide/02-hosting-in-aspnetcore.md`, and verify they describe separate Elsa contexts, independent migration histories, and safe coexistence with a domain `DbContext`.
- [ ] 3.2 Update the package list and host-wiring example in `doc/guide/appendix-cheat-sheet.md`, and verify the obsolete `UseEntityFrameworkPersistence(...)` placeholder is removed and the provider package names match the revised chapter.

## 4. Validate the completed revision

- [ ] 4.1 Compile every revised C# persistence snippet against Elsa 3.10.2 and verify there are no missing package references, obsolete context names, or unsupported helper calls.
- [ ] 4.2 Run an isolated SQLite restart/resume smoke check with both persistence boundaries configured, and verify a suspended instance and its bookmark survive restart and can resume.
- [ ] 4.3 Scan the revised guide for stale persistence claims, including singular `ElsaDbContext` guidance, `UseEntityFrameworkPersistence`, and the claim that SQL Server has no Elsa-specific package, and verify no active guidance contains them.
- [ ] 4.4 Review the final diff against `specs/documentation/persistence-guide/spec.md` and `design.md`, and verify all acceptance scenarios are covered while revisions #2-#5 remain deferred and unimplemented.

## Workflow follow-up

- After implementation and review are complete, archive the change with `/opsx-archive`.
- Verify the archived capability spec and deferred backlog remain traceable.
