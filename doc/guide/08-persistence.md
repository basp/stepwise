# 08 — Persistence

By default, Elsa is **in-memory only**: instances, bookmarks, and definitions vanish on restart. For any real product, configure durable persistence. This chapter covers EF Core (the most common) and the alternatives.

## Choosing a provider

| Provider | Package | Use when |
|----------|---------|----------|
| EF Core (SQL Server/PostgreSQL/SQLite/MySQL) | `Elsa.Persistence.EFCore` + DB provider | Default for relational stores; migrations, LINQ |
| MongoDB | `Elsa.Persistence.MongoDb` | Document store, flexible schema |
| Dapper | `Elsa.Persistence.Dapper` | Lightweight SQL, no EF overhead |
| Elasticsearch | `Elsa.Persistence.Elasticsearch` | Search/analytics-heavy |
| In-memory | (default) | Dev, tests |

## EF Core setup (minimal)

Persistence is configured per feature (runtime, definitions, instances, labels, identity, alterations, tenants). The common pattern for a code-first host:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseDefaultRuntime();
        runtime.UseEntityFrameworkCore(ef =>
        {
            // SQL Server (needs Microsoft.EntityFrameworkCore.SqlServer):
            ef.DbContextOptionsBuilder = (_, options) =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Elsa"));

            // PostgreSQL (needs Elsa.Persistence.EFCore.PostgreSQL):
            // ef.UsePostgreSql(builder.Configuration.GetConnectionString("Elsa"));

            ef.RunMigrations = true;   // apply Elsa's migrations at startup (dev/first deploy)
        });
    });
    // ... features (UseHttp, UseScheduling, ...)
});
```

Notes:

- `UseEntityFrameworkCore` is called on the **feature** (`WorkflowRuntimeFeature`), not on `IModule`. Hence it lives inside `UseWorkflowRuntime(...)`.
- `DbContextOptionsBuilder` is `Action<IServiceProvider?, DbContextOptionsBuilder>`. Assign a delegate that calls `UseSqlServer`/`UseNpgsql`/`UseSqlite`.
- For PostgreSQL, the `Elsa.Persistence.EFCore.PostgreSQL` package adds `UsePostgreSql(feature, connectionString, ...)` extension methods that set the builder and migrations assembly for you.
- For SQL Server there is no Elsa-specific package; reference `Microsoft.EntityFrameworkCore.SqlServer` and set the builder as above. Migrations assembly defaults to `Elsa.Persistence.EFCore`.
- Definitions/instances/labels/identity/alterations each have their own `UseEntityFrameworkCore(...)` on their respective features if you want them persisted (most apps persist at least runtime + instances + definitions).

## Migrations

Elsa ships its own DbContext (`ElsaDbContext`) with migrations. Apply them:

```bash
dotnet ef database update --project ... --context ElsaDbContext
```

Or programmatically at startup (dev only):

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ElsaDbContext>();
    await db.Database.MigrateAsync();
}
```

For custom migrations / multiple DbContexts, see [EF Core Migrations](https://docs.elsaworkflows.io/guides/persistence/ef-migrations.md).

## Separate DbContexts

**Do not** reuse your domain `DbContext` for Elsa. Elsa's schema (definitions, instances, bookmarks, execution logs, …) is large and evolves with Elsa versions. Keep them separate:

- `AppDbContext` — your domain.
- `ElsaDbContext` — engine state.

This avoids migration conflicts and lets you upgrade Elsa independently.

## What gets stored

Stores (each backed by the provider):

- `IWorkflowDefinitionStore` — definitions (if stored/published, not code-only).
- `IWorkflowInstanceStore` — instances.
- `IBookmarkStore` — bookmarks.
- `ITriggerStore` — triggers.
- `IActivityExecutionStore` / execution logs.
- `IWorkflowExecutionLogStore`.
- `IKeyValueStore` — runtime coordination (distributed locks, outbox).

## In-memory vs durable: decision matrix

| Scenario | Persistence |
|----------|-------------|
| Unit tests | In-memory (default) |
| Local dev / prototyping | In-memory or SQLite |
| Single-node prod, restarts OK to lose state | SQLite/SQL Server |
| Multi-node / long-running / must-survive-restart | SQL Server/PostgreSQL + durable scheduler + distributed locks |

## Distributed locks & caching (multi-node)

For clustered deployments you need:

- A **distributed lock** provider (DB-backed or Redis) so resume doesn't duplicate.
- **Distributed cache** (Redis) for definition/instance caching across nodes.
- A **durable scheduler** (Quartz/Hangfire) so timers fire on any node.

See [Distributed Hosting](https://docs.elsaworkflows.io/hosting/distributed-hosting.md) and [Clustering](https://docs.elsaworkflows.io/guides/clustering.md).

## Retention

Completed instances, execution logs, and incidents accumulate. Prune them with the separate **`Elsa.Retention`** package (not part of core), which runs a recurring cleanup job:

```bash
dotnet add package Elsa.Retention --version 3.10.x
```

```csharp
using Elsa.Retention.Extensions;
using Elsa.Retention.Models;

elsa.UseRetention(retention =>
{
    retention.SweepInterval = TimeSpan.FromHours(4);
    retention.AddDeletePolicy("Finished workflows older than 30 days", sp =>
        new RetentionWorkflowInstanceFilter
        {
            WorkflowStatus = WorkflowStatus.Finished,
            // + TimestampFilters on FinishedAt ...
        });
});
```

See [Retention](https://docs.elsaworkflows.io/optimize/retention.md). Without retention, your DB grows unbounded and queries slow down. The cleanup job is a `[SingleNodeTask]`, so configure a shared distributed lock in multi-node deployments.

## Indexing

Elsa's common query patterns benefit from indexes. The official [Indexing Notes](https://docs.elsaworkflows.io/guides/persistence/indexing-notes.md) lists recommended indexes per provider. Apply them in production.

## Storage boundaries & tenants

In multitenant setups, storage is isolated per tenant (or shared, depending on config). See [Multitenancy](https://docs.elsaworkflows.io/multitenancy/introduction.md).

## Backups

Treat `ElsaDbContext` like any critical database: backups, point-in-time recovery, migration testing. Losing it loses in-flight workflow state.

## Next

Continue with [09 — Custom activities](09-custom-activities.md).
