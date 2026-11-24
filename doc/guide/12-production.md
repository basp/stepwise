# 12 — Operating in production

This chapter is a checklist-oriented overview. Each topic has a full official guide; links are provided.

## Pre-flight checklist

- [ ] Durable persistence configured (EF Core + real DB).
- [ ] Migrations applied; backup strategy in place.
- [ ] Durable scheduler (Quartz or Hangfire) for timers.
- [ ] Distributed locks + distributed cache if multi-node.
- [ ] Retention configured.
- [ ] Health checks enabled.
- [ ] Observability: structured logs + OpenTelemetry.
- [ ] Auth configured (Elsa API permissions / OIDC / API keys).
- [ ] HTTPS, proxy headers, rate limiting.
- [ ] `BaseUrl` points at the externally reachable URL.

## Clustering & distributed hosting

- Run multiple nodes behind a load balancer.
- Shared DB (persistence) + distributed locks (avoid duplicate resumes).
- Distributed cache for definition/instance caches (Redis).
- Durable scheduler with clustering enabled (Quartz clustering) so timers fire once.
- Proto.Actor runtime is an option for actor-based coordination (advanced). See [Proto.Actor](https://docs.elsaworkflows.io/guides/architecture/protoactor-workflow-runtime.md).

See [Distributed Hosting](https://docs.elsaworkflows.io/hosting/distributed-hosting.md) and [Clustering](https://docs.elsaworkflows.io/guides/clustering.md).

## Schedulers

| Scheduler | Package | When |
|-----------|---------|------|
| In-memory (default) | `Elsa.Scheduling` | Dev only |
| Quartz.NET | `Elsa.Scheduling.Quartz` | Production, clustered |
| Hangfire | `Elsa.Scheduling.Hangfire` | If you already use Hangfire |

## Health checks

Elsa provides readiness checks for runtime, persistence, and distributed locks:

```csharp
builder.Services.AddHealthChecks()
    .AddElsaReadinessChecks(
        includePersistence: true,
        includeDistributedLocks: true);
app.MapHealthChecks("/health");
```

See [Readiness and Health Checks](https://docs.elsaworkflows.io/operate/readiness-and-health-checks.md).

## Runtime administration (drain/pause)

Pause/resume/drain a runtime for deploys:

```csharp
// via IRuntime / admin API
```

See [Runtime Administration](https://docs.elsaworkflows.io/operate/runtime-administration.md). Use draining during deploys to let in-flight bursts finish.

## Observability

- **Structured logs**: configure capture, redaction, durable storage. See [Structured Logs](https://docs.elsaworkflows.io/operate/structured-logs.md).
- **Console logs**: raw capture + SignalR streaming to Studio. See [Console Logs](https://docs.elsaworkflows.io/operate/console-logs.md).
- **OpenTelemetry**: workflow/activity spans. See [Distributed Tracing](https://docs.elsaworkflows.io/operate/distributed-tracing.md).
- **Incidents**: faults, retries, recovery. See [Incidents](https://docs.elsaworkflows.io/operate/incidents.md).
- **Monitoring & observability overview**: [link](https://docs.elsaworkflows.io/operate/monitoring-observability.md).

## Security

- Auth: Elsa Identity, API keys, OIDC, external auth broker. See [Authentication](https://docs.elsaworkflows.io/guides/authentication.md).
- HTTP endpoint security: separate workflow ingress auth from Elsa API permissions. See [HTTP Endpoint Security](https://docs.elsaworkflows.io/guides/security/http-endpoint-security.md).
- Rate limiting: [Elsa API Rate Limiting](https://docs.elsaworkflows.io/guides/security/elsa-api-rate-limiting.md).
- Bookmark resume tokens: expirations, single-use, revocation. See [Bookmark Resume Tokens](https://docs.elsaworkflows.io/guides/security/bookmark-resume-tokens.md).
- Secrets: store/resolve sensitive values. See [Secrets Management](https://docs.elsaworkflows.io/guides/security/secrets-management.md).
- Production hardening checklist: [link](https://docs.elsaworkflows.io/guides/security/production-hardening.md).

## Deployment

- Containers/Docker Compose: [link](https://docs.elsaworkflows.io/getting-started/containers.md).
- Kubernetes: [link](https://docs.elsaworkflows.io/guides/kubernetes-deployment.md).
- Configuration management: [link](https://docs.elsaworkflows.io/guides/deployment/configuration-management.md).

## Performance tuning

- Worker count: [link](https://docs.elsaworkflows.io/optimize/workers.md).
- Throughput tuning: [link](https://docs.elsaworkflows.io/guides/performance/throughput-tuning.md).
- Log persistence: [link](https://docs.elsaworkflows.io/optimize/log-persistence.md).

## Multitenancy

If you're building SaaS, configure tenant resolution early — retrofitting is painful. See [Multitenancy](https://docs.elsaworkflows.io/multitenancy/introduction.md) and [Setup](https://docs.elsaworkflows.io/multitenancy/setup.md).

## Studio (optional but recommended)

Even code-first teams benefit from Studio for inspecting instances, execution logs, and incidents. Host it standalone or embedded. See [Elsa Studio](https://docs.elsaworkflows.io/application-types/elsa-studio.md) and [Integration](https://docs.elsaworkflows.io/guides/studio/integration.md).

## Next

You've completed the guide. Keep the [cheat sheet](appendix-cheat-sheet.md) handy, and revisit chapters as needed. For deeper dives, the official docs index is at [docs.elsaworkflows.io](https://docs.elsaworkflows.io/).
