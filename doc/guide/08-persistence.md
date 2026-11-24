# 08 — Persistence

By default, Elsa stores workflow state **in memory**. Code-first definitions registered with `AddWorkflow<T>()` are recreated from source after a restart, but stored definitions, workflow instances, bookmarks, and runtime records are lost. If a product must resume work after a process replacement, configure durable persistence.

This chapter targets **Elsa 3.10.2** and the guide's **.NET 11** host. The companion sample remains on the in-memory defaults; the examples below show how to add durability to your own host.

## Choosing a provider

Elsa 3.10.2 organizes workflow persistence into two boundaries: **workflow management** and **workflow runtime**. The EF Core provider packages configure both when you register persistence under each feature.

| Provider | Elsa package | Helper | Package assets | Operational boundary |
|----------|-------------|--------|----------------|----------------------|
| SQLite | `Elsa.Persistence.EFCore.Sqlite` | `UseSqlite` | `net8.0`, `net9.0`, `net10.0` | Good for local durable development and low-concurrency single-node use; limited write concurrency |
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `UseSqlServer` | `net8.0`, `net9.0`, `net10.0` | General-purpose production relational store; supports Elsa's workflow commit transaction |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `UsePostgreSql` | `net8.0`, `net9.0`, `net10.0` | General-purpose production relational store |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `UseMySql` | `net8.0`, `net9.0` only | **Not a supported .NET 11 target in Elsa 3.10.2**; see the warning below |

Keep every `Elsa.*` package on the same version. The provider packages bring in the matching vendor EF Core provider, so you do not need a separate SQL Server, Npgsql, SQLite, or Pomelo package merely to call the Elsa helper. The package ID is `PostgreSql`, not `PostgreSQL` or `Npgsql`.

> **MySQL on .NET 11:** `Elsa.Persistence.EFCore.MySql` 3.10.2 only ships `net8.0` and `net9.0` assets. A .NET 11 build selects the `net9.0` asset as an unsupported fallback and restores Pomelo 9.0.0 alongside EF Core 10.0.9, producing an `NU1608` version-constraint warning. The helper compiles, but do not treat this as a supported production configuration. Use a provider with a supported target, or wait for a compatible MySQL package.

### Alternative providers

| Provider | Package | Coverage in 3.10.2 | Boundaries |
|----------|---------|-------------------|------------|
| MongoDB | `Elsa.Persistence.MongoDb` | Core management stores (definitions and instances) and runtime stores (triggers, bookmarks, bookmark queue, execution logs, key-values) | No EF migrations; Elsa creates indexes through hosted services. The 3.10.2 provider does not include a bookmark dead-letter store |
| Dapper | `Elsa.Persistence.Dapper` | Core management and runtime stores, including bookmark queue and dead-letter records | Uses FluentMigrator or externally managed SQL schema. Built-in connection providers cover SQLite, SQL Server, and PostgreSQL; there is no built-in MySQL provider |
| Elasticsearch | `Elsa.Persistence.Elasticsearch` | **Only** `IWorkflowInstanceStore` and `IWorkflowExecutionLogStore` | Not a full persistence replacement. Definitions, bookmarks, triggers, queues, activity execution records, key-values, and other stores remain separately configured |
| In-memory | Default | Process-local stores | Suitable for tests and disposable runs; state is lost when the process exits |

Installing an alternative package is not enough: select it for the management and/or runtime features you intend to replace. A mixed-provider deployment is valid, but document which provider owns each store.

## The two persistence boundaries

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

| Boundary | EF Core context | Stores configured by the combined feature |
|----------|----------------|-------------------------------------------|
| Management | `ManagementElsaDbContext` | Workflow definitions, definition-registry generations, workflow instances |
| Runtime | `RuntimeElsaDbContext` | Triggers, bookmarks, bookmark queue items, bookmark dead-letter items, workflow execution logs, activity execution records, key-value pairs |

Both contexts live in the `Elsa.Persistence.EFCore.Modules` namespace, under `.Management` and `.Runtime` respectively.

Important distinctions:

- **Runtime-only persistence does not make definitions or instances durable.** They still use the in-memory management stores unless you also configure `UseWorkflowManagement(...)`.
- **Management-only persistence is also incomplete.** A stored instance without durable bookmarks, triggers, and runtime records cannot reliably resume after restart.
- The **activity execution queue** is an in-process burst mechanism. `ActivityExecutionRecords` are execution evidence, not queued work waiting to run.
- The active durable queue is the **bookmark queue**, including dead-letter items. `RuntimeElsaDbContext` retains a legacy inbox table for schema compatibility, but EF Core 3.10.2 does not register an active durable inbox store.
- Key-value records back the default workflow dispatch outbox and generic coordination state. They do **not** automatically provide distributed locks; configure a distributed lock provider separately for clustered hosts.

Identity, labels, alterations, and tenants have their own feature-specific persistence configuration. The two workflow contexts above do not configure those optional modules.

## Complete durable SQLite configuration

### Packages and connection string

```xml
<ItemGroup>
  <PackageReference Include="Elsa" Version="3.10.2" />
  <PackageReference Include="Elsa.Persistence.EFCore.Sqlite" Version="3.10.2" />
</ItemGroup>
```

```json
{
  "ConnectionStrings": {
    "Elsa": "Data Source=elsa.db;Cache=Shared;"
  }
}
```

Use a file-backed connection string, not `Data Source=:memory:`, when state must survive restart. If you choose a nested file path, create its directory before startup.

### Program.cs

This is the production-shaped configuration: both persistence boundaries are durable, and automatic startup migrations are explicitly disabled. Apply the migrations through your deployment process as shown in [Migrations](#migrations).

```csharp
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Elsa")
    ?? throw new InvalidOperationException("Connection string 'Elsa' not found.");

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management =>
        management.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlite(connectionString);
            ef.RunMigrations = false; // Production: apply migrations in the deployment pipeline.
        }));

    elsa.UseWorkflowRuntime(runtime =>
        runtime.UseEntityFrameworkCore(ef =>
        {
            ef.UseSqlite(connectionString);
            ef.RunMigrations = false; // Production: apply migrations in the deployment pipeline.
        }));
});

var app = builder.Build();
app.Run();
```

The provider helper belongs **inside** `UseEntityFrameworkCore(...)`. It configures the migrations assembly, history table, schema, and SQLite-specific model behavior. Calling a provider helper directly on the management or runtime feature is not the supported API shape.

The two features can use the same connection string and therefore the same physical database. They still use separate EF Core contexts and separate migration streams. Use different connection strings if you want management and runtime data in separate databases.

For local development only, you can set `ef.RunMigrations = true` on both features. Do not use that setting as the production migration strategy.

## Swapping the EF Core provider

Keep the same two-boundary structure. Replace the package and both helper calls:

| Provider | Package reference | Management call | Runtime call |
|----------|-------------------|-----------------|--------------|
| SQL Server | `Elsa.Persistence.EFCore.SqlServer` | `ef.UseSqlServer(connectionString)` | `ef.UseSqlServer(connectionString)` |
| PostgreSQL | `Elsa.Persistence.EFCore.PostgreSql` | `ef.UsePostgreSql(connectionString)` | `ef.UsePostgreSql(connectionString)` |
| MySQL | `Elsa.Persistence.EFCore.MySql` | `ef.UseMySql(connectionString)` | `ef.UseMySql(connectionString)` |

All four helpers are in `Elsa.Persistence.EFCore.Extensions`. SQL Server, PostgreSQL, and SQLite have `net10.0` assets suitable for the guide's .NET 11 target. MySQL does not; retain the explicit unsupported-target warning if you document that swap.

The configuration shape is shared, but the operational characteristics are not. SQLite is convenient for local durability but has limited write concurrency. Choose SQL Server or PostgreSQL for clustered or higher-throughput production deployments, and validate the selected provider under your expected load.

## Migrations

Elsa ships EF Core migrations in each provider package. The helpers point each context at the correct migrations assembly.

### Development

`RunMigrations` defaults to `true` in Elsa 3.10.2. The production example sets it to `false` explicitly; for local development you may set it to `true` on both persistence features, or apply the migrations with the CLI.

### Production

Install `dotnet-ef` with a version compatible with the EF Core packages resolved by your host (10.0.x for the validated Elsa 3.10.2 restore), then apply both contexts in a controlled deployment step:

```bash
dotnet ef database update --project <startup-project> --startup-project <startup-project> --context ManagementElsaDbContext
dotnet ef database update --project <startup-project> --startup-project <startup-project> --context RuntimeElsaDbContext
```

Use a deployment identity with permission to apply schema changes, review generated SQL, test migrations in staging, and keep backups. Do not rely on application startup to race migrations across multiple production nodes.

### Separate streams, shared history table

`ManagementElsaDbContext` and `RuntimeElsaDbContext` have separate context-specific migration sets. When both target the same physical database, they share the default `Elsa.__EFMigrationsHistory` table. Running both commands is still required because each context tracks a different migration stream.

If you need separate physical history tables, configure different `ElsaDbContextOptions.MigrationsHistoryTableName` values for management and runtime. Separate connection strings also give each database its own history table.

## Restart semantics

What survives a restart depends on which stores are durable:

| Configuration | After a process restart |
|---------------|-------------------------|
| Default in-memory | Code-first definitions are recreated from registered source. Stored definitions, instances, bookmarks, triggers, queues, logs, and key-values are lost |
| Runtime persistence only | Runtime records may survive, but definitions and instances remain in memory. Durable bookmarks cannot resume an instance that no longer exists |
| Management persistence only | Stored definitions and instances survive, but bookmarks, triggers, queues, logs, and coordination records do not. Suspended workflows cannot reliably resume |
| Management + runtime persistence | Definitions, instances, bookmarks, and runtime records survive, subject to provider durability and correct deployment operations |

A **code-first definition** is materialized from the workflow class registered with `AddWorkflow<T>()`. A **stored definition** is created or edited through Elsa's management APIs and depends on the management store. Configuring runtime persistence alone does not turn the former into the latter or make instances durable.

### In-memory vs durable decision matrix

| Scenario | Recommended persistence |
|----------|-------------------------|
| Unit tests and disposable scripts | In-memory |
| Local prototyping where restart loss is acceptable | In-memory |
| Local development that must survive restart | SQLite with both boundaries |
| Single-node production that must resume work | SQLite for low concurrency, or SQL Server/PostgreSQL |
| Multi-node, long-running, or higher-throughput production | SQL Server/PostgreSQL with both boundaries, distributed locks, shared cache, and a durable scheduler |

Durable persistence is necessary for restart survival, but it is not sufficient for a clustered deployment. Configure distributed locking to prevent duplicate resumes, a shared cache where needed, and a durable scheduler such as Quartz or Hangfire for timers.

## Adding Elsa to an existing application

Keep Elsa's contexts separate from your domain `DbContext`:

- `AppDbContext` owns your domain model and migration history.
- `ManagementElsaDbContext` owns workflow definitions, registry data, and instances.
- `RuntimeElsaDbContext` owns bookmarks, triggers, queues, logs, and key-value coordination records.

Do **not** reuse the domain context for Elsa stores. Elsa's model evolves with the engine, and mixing it into the application's migration history makes upgrades harder to reason about.

A shared physical database is fine. The contexts keep separate models and migration streams, even though they share the default EF Core history table when co-located. Use separate databases if your backup, security, or operational requirements call for independent storage.

Optional Elsa features keep their own persistence ownership. Enable their feature-specific persistence only when you use them; do not assume the two workflow contexts cover identity, labels, alterations, or tenants.

## Backups and provider operations

Treat both Elsa contexts as critical application data:

- Back up management and runtime data together, or coordinate consistent snapshots if they live in separate databases.
- Include the migration history and any provider-specific objects required by your deployment.
- Quiesce writes or use the database's native snapshot/backup facilities so definitions, instances, bookmarks, and logs represent a consistent point in time.
- Test restores regularly, including resuming a suspended workflow after restore.
- Keep the deployed Elsa package version aligned with the schema version you tested.

For SQLite, use the SQLite backup API or stop the host before copying the database file. For SQL Server and PostgreSQL, use their native backup and point-in-time recovery tools. For MongoDB or Elasticsearch, use the provider's snapshot and restore facilities and verify index mappings and refresh behavior.

Provider boundaries matter operationally: SQLite is not a clustered database, MySQL 3.10.2 is not supported on .NET 11, and Elasticsearch only covers the instance and workflow-execution-log stores. A shared connection string does not make providers operationally interchangeable.

## Retention and indexing

Completed instances, execution logs, and queue records accumulate. Configure the separate `Elsa.Retention` extension for the cleanup policies your product needs, and include related runtime records in the cleanup design. Retention does not replace backups or an archive strategy.

Review the official [Indexing Notes](https://docs.elsaworkflows.io/guides/persistence/indexing-notes.md) for provider-specific indexes. Elsa's built-in migrations include core indexes, but production workloads may need additional indexes based on their query patterns.

## Storage boundaries and tenants

In multitenant deployments, configure tenant resolution before workflow data is written. Verify that every selected provider preserves tenant isolation for definitions, instances, bookmarks, triggers, queues, logs, and key-values. A mixed-provider setup must make tenant ownership explicit for each store.

## Next

Continue with [09 — Custom activities](09-custom-activities.md).
