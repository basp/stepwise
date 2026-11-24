# Appendix — Persistence provider reference

This reference collects provider-specific package, compatibility, and operational details for the persistence concepts in [chapter 08](08-persistence.md). It targets Elsa 3.10.2 and the guide's .NET 11 host; recheck package assets and provider support when upgrading Elsa.

## EF Core providers

| Provider | Elsa package | Helper | Package assets | Operational notes |
|----------|--------------|--------|----------------|-------------------|
| SQLite | `Elsa.Persistence.EFCore.Sqlite` | `UseSqlite` | `net8.0`, `net9.0`, `net10.0` | Useful for local durability and low-concurrency single-node use; limited write concurrency |
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `UseSqlServer` | `net8.0`, `net9.0`, `net10.0` | Supports Elsa's workflow commit transaction |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `UsePostgreSql` | `net8.0`, `net9.0`, `net10.0` | General-purpose relational provider |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `UseMySql` | `net8.0`, `net9.0` | Not a supported .NET 11 target in Elsa 3.10.2 |

Keep every `Elsa.*` package on the same version. The provider packages bring in the matching vendor EF Core provider; the Elsa package ID for PostgreSQL is `PostgreSql` (not `PostgreSQL` or `Npgsql`). The helpers are in `Elsa.Persistence.EFCore.Extensions` and belong inside `UseEntityFrameworkCore(...)` for both management and runtime.

> **MySQL on .NET 11:** `Elsa.Persistence.EFCore.MySql` 3.10.2 only ships `net8.0` and `net9.0` assets. A .NET 11 build selects the `net9.0` asset as an unsupported fallback and restores Pomelo 9.0.0 alongside EF Core 10.0.9, producing an `NU1608` version-constraint warning. The helper compiles, but this is not a supported production configuration. Use a provider with a supported target or wait for a compatible MySQL package.

To switch the SQLite example in chapter 08 to another EF Core provider, use the corresponding package and helper for both persistence features:

| Provider | Package reference | Management and runtime helper |
|----------|-------------------|-------------------------------|
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `ef.UseSqlServer(connectionString)` |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `ef.UsePostgreSql(connectionString)` |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `ef.UseMySql(connectionString)` |

The configuration shape is shared, but operational characteristics are not. Validate the selected provider under expected load; SQLite is not a clustered database, while SQL Server and PostgreSQL are common choices for clustered or higher-throughput deployments.

## Alternative providers

Installing an alternative package is not enough: select it for the management and/or runtime features you intend to replace. Mixed-provider deployments are valid, but document which provider owns each store.

| Provider | Package | Coverage in 3.10.2 | Boundaries and caveats |
|----------|---------|--------------------|------------------------|
| MongoDB | `Elsa.Persistence.MongoDb` | Core management stores (definitions and instances) and runtime stores (triggers, bookmarks, bookmark queue, execution logs, key-values) | No EF migrations; hosted services create indexes. Definition-registry generation coordination remains process-local, and this provider does not include a bookmark dead-letter store |
| Dapper | `Elsa.Persistence.Dapper` | Core management and runtime stores, including bookmark queue and dead-letter records | Uses FluentMigrator or externally managed SQL schema. Definition-registry generation coordination remains process-local. Built-in connection providers cover SQLite, SQL Server, and PostgreSQL, not MySQL |
| Elasticsearch | `Elsa.Persistence.Elasticsearch` | `IWorkflowInstanceStore` and `IWorkflowExecutionLogStore` only | Not a full persistence replacement; configure definitions, bookmarks, triggers, queues, activity execution records, key-values, and other stores separately |
| In-memory | Default | Process-local stores | Suitable for tests and disposable runs; state is lost when the process exits |

## Migration history

`ManagementElsaDbContext` and `RuntimeElsaDbContext` use separate migration sets. When both target the same physical database, they share the default `__EFMigrationsHistory` table; schema-capable providers place it in the `Elsa` schema, while SQLite uses the unqualified table name. Apply both migration streams as described in [chapter 08](08-persistence.md#migrations).

To use separate physical history tables, configure different `ElsaDbContextOptions.MigrationsHistoryTableName` values for management and runtime. Separate connection strings also give each database its own history table.

## Backup and indexing

Back up management and runtime data together, or coordinate consistent snapshots when they live in separate databases. Include migration history and provider-specific objects, and test restores by resuming a suspended workflow. Keep the deployed Elsa package version aligned with the schema version you tested.

- **SQLite:** use the SQLite backup API or stop the host before copying the database file.
- **SQL Server and PostgreSQL:** use native backup and point-in-time recovery tools.
- **MongoDB and Elasticsearch:** use provider snapshot and restore facilities; verify index mappings and refresh behavior.

Elsa's built-in migrations include core indexes, but production workloads may need additional indexes for their query patterns. Review the official [Indexing Notes](https://docs.elsaworkflows.io/guides/persistence/indexing-notes.md). A shared connection string does not make providers operationally interchangeable; for example, Elasticsearch only covers the stores listed above.