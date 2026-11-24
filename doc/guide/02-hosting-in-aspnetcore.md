# 02 — Hosting Elsa in ASP.NET Core

This chapter turns an empty ASP.NET Core app into an Elsa host. It mirrors the sample project in this repo (`src/Stepwise.Server`), which uses Elsa 3.10.2 on .NET 11.

## Packages

The meta-package `Elsa` pulls in the core engine. Add feature packages for what you use.

```xml
<ItemGroup>
  <PackageReference Include="Elsa" Version="3.10.2" />
  <PackageReference Include="Elsa.Http" Version="3.10.2" />        <!-- HTTP activities/triggers -->
  <PackageReference Include="Elsa.Scheduling" Version="3.10.2" />  <!-- Delay, timers -->
  <!-- Add one Elsa.Persistence.EFCore.<Provider> package for durable state (chapter 08) -->
</ItemGroup>
```

Version alignment matters: keep all `Elsa.*` packages on the same version.

## Minimal Program.cs

```csharp
using Elsa.Extensions;
using Stepwise.Server.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    // 1. Register your code-first workflow definitions.
    elsa.AddWorkflow<EchoWorkflow>();
    elsa.AddWorkflow<Test>();

    // 2. Enable features (each UseX adds a feature to the module).
    elsa.UseScheduling();

    elsa.UseHttp(http =>
    {
        http.ConfigureHttpOptions = options =>
        {
            options.BaseUrl = new Uri("http://localhost:5268");
            options.BasePath = "/workflows";
        };
    });
});

var app = builder.Build();

// 3. Wire Elsa into the HTTP pipeline (endpoints, middleware).
app.UseWorkflows();

app.Run();
```

Three things to notice:

1. **`AddElsa`** configures the module. Inside, you register workflows (`AddWorkflow<T>`) and enable features (`UseScheduling`, `UseHttp`, …). Each `UseXyz()` is a feature that registers services, activities, and options.
2. **Feature packages must be enabled.** Installing `Elsa.Http` alone does nothing until you call `UseHttp`. This is the module/feature system.
3. **`UseWorkflows()`** maps the HTTP endpoints Elsa needs (trigger routes, API). Without it, HTTP triggers won't respond.

## What `AddElsa` actually does

Conceptually:

- Creates an `IModule` (the Elsa configuration root).
- Registers core engine services: `IWorkflowRunner`, `IWorkflowRuntime`, `IWorkflowDispatcher`, activity registry, definition store, instance store.
- Your `AddWorkflow<T>` calls register a **workflow provider** that yields that definition.
- `UseX` calls register feature-specific services and activities.

By default, persistence is **in-memory**. Code-first definitions registered with `AddWorkflow<T>()` are recreated from source after a restart, but stored definitions, instances, bookmarks, and runtime records are lost. For durable state, configure both workflow management and workflow runtime persistence as shown in [chapter 08](08-persistence.md).

EF Core persistence uses two separate contexts: `ManagementElsaDbContext` for definitions and instances, and `RuntimeElsaDbContext` for bookmarks, triggers, queues, logs, and key-value coordination records. They have separate migration streams; when they share a database, they share the default `__EFMigrationsHistory` table unless you configure separate history table names. Keep both contexts separate from your domain `DbContext`.

## Registering workflows

Code-first workflows are classes deriving from `WorkflowBase`:

```csharp
elsa.AddWorkflow<EchoWorkflow>();
```

You can also register by type at runtime or load from JSON/ElsaScript. For now, `AddWorkflow<T>` is the idiomatic code-first path.

## HTTP options

`BasePath` sets the route prefix under which HTTP triggers are exposed. With the sample config, the `EchoWorkflow`'s `/echo` endpoint is reachable at `http://localhost:5268/workflows/echo` — but note the host/port come from ASP.NET Core's server binding (`launchSettings.json` / `applicationUrl`), not from Elsa.

`BaseUrl` is **not** used for routing. It is used to *publish/generate* absolute URLs (bookmark and signal trigger URLs produced outside an HTTP request, and the endpoint path shown in Elsa Studio). Set it to the externally reachable URL in production (behind proxies/load balancers); if it is wrong, generated links/emails point at the wrong host, but the endpoint itself still responds at the ASP.NET Core-bound address.

## Middleware ordering

```csharp
app.UseWorkflows();
```

Call it after auth/routing setup you need, but Elsa's own endpoint mapping happens inside. If you add custom middleware (auth, rate limiting), place it so Elsa endpoints are covered. See the security guides for production hardening.

## Console host (no ASP.NET)

Elsa works without ASP.NET — useful for workers, tests, scripts:

```csharp
var services = new ServiceCollection();
services.AddElsa(elsa => { }); // in-memory, no HTTP features
var sp = services.BuildServiceProvider();
var runner = sp.GetRequiredService<IWorkflowRunner>();
await runner.RunAsync(new Sequence { Activities = { new WriteLine("hi") } });
```

## Hosting in an *existing* app

If you're adding Elsa to an app that already has its own DI, DbContext, auth, etc., the pitfalls are: duplicate DbContext registration, tenant resolution, and middleware ordering. The official [Hosting Elsa in an Existing App](https://docs.elsaworkflows.io/guides/onboarding/hosting-elsa-in-existing-app.md) guide covers these. Key points:

- Elsa needs its own `ManagementElsaDbContext` and `RuntimeElsaDbContext` when you enable EF Core persistence; do not reuse your domain `DbContext` for Elsa stores.
- The two Elsa contexts may share a physical database while retaining separate models and migration streams. Their default EF Core history table is shared when co-located; configure distinct history table names if you need physical separation.
- Register Elsa *after* your core services so it can resolve them if custom activities need them.

## Configuration via appsettings

Elsa reads configuration from the `Elsa` section (when using the config-driven hosting). For code-first hosts, options are set in code as above. For larger apps, prefer binding options from `IConfiguration`:

```csharp
elsa.UseHttp(http => http.ConfigureHttpOptions = o =>
{
    o.BaseUrl = new Uri(builder.Configuration["Elsa:BaseUrl"]!);
});
```

## Verifying the host

Run the app and hit an HTTP trigger:

```bash
dotnet run --project src/Stepwise.Server
curl http://localhost:5268/workflows/echo
```

You should see `Hello!` in the console and an HTTP 200 (or the response configured by `WriteHttpResponse`).

## Next

Continue with [03 — Authoring workflows in code](03-authoring-workflows.md).
