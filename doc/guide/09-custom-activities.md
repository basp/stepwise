# 09 — Custom activities

When built-ins don't fit, write your own activity. This is the main extensibility point and where Elsa becomes *your* engine.

## Activity base classes

| Base | Use for |
|------|---------|
| `Activity` | Sync/async work, no return value |
| `CodeActivity` / `CodeActivity<T>` | Run a delegate; produce output `T` |
| `Activity<T>` (with `ExecuteAsync`) | Full control |
| Implement `IActivity` | Maximum control (rare) |

## Simplest: `CodeActivity`

```csharp
public class SendEmail : CodeActivity
{
    public Input<string> To { get; set; } = default!;
    public Input<string> Subject { get; set; } = default!;
    public Input<string> Body { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var to = context.Get(To);
        var subject = context.Get(Subject);
        var body = context.Get(Body);

        await _mailer.SendAsync(to, subject, body);
        await context.CompleteActivityAsync();
    }
}
```

Read inputs via `context.Get(input)` (or `input.Get(context)`). Always call `CompleteActivityAsync()` when done, or the activity stays "running".

## Producing output

```csharp
public class GetOrder : CodeActivity<Order>
{
    public Input<string> OrderId { get; set; } = default!;

    protected override async ValueTask<Order> ExecuteAsync(ActivityExecutionContext context)
    {
        var id = context.Get(OrderId);
        return await _orders.GetAsync(id);
    }
}
```

The returned value becomes the activity output. Bind it to a variable:

```csharp
var orderVar = builder.WithVariable<Order>();
// ...
new GetOrder { OrderId = new(orderIdVar), Result = new(orderVar) }  // if Result output property exists
```

Or read via `activity.GetOutput<Order>(context)`.

## Inputs: `Input<T>` and attributes

```csharp
[ActivityInput(Hint = "The recipient email address")]
public Input<string> To { get; set; } = default!;

[ActivityOutput]
public Output<Order> Result { get; set; } = default!;
```

Attributes (`ActivityInput`, `ActivityOutput`, `ActivityProperty`) drive Studio metadata and serialization. In code-first-only activities they're optional but recommended.

## Blocking activity (bookmark)

```csharp
public class AwaitApproval : Activity
{
    public Input<string> RequestId { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var requestId = context.Get(RequestId);
        context.CreateBookmark(new CreateBookmarkArgs
        {
            BookmarkName = "AwaitApproval",
            Payload = new { RequestId = requestId },
        });
        // do NOT complete — activity stays suspended
    }

    // called on resume (if Callback set) or override ResumeAsync
}
```

When resumed, the engine calls the activity's resume path. Override `ResumeAsync` or set a `Callback` in `CreateBookmarkArgs`.

## Trigger activity

Implement `ITrigger` and mark it startable:

```csharp
[Activity("MyCompany", "Integrations", "Fires when a webhook arrives")]
public class WebhookTrigger : Activity, ITrigger
{
    public Input<string> Path { get; set; } = default!;

    // ITrigger members: get trigger name/payload/hash
    public string GetTriggerName() => "Webhook";
    public object? GetTriggerPayload(ActivityExecutionContext ctx) => new { Path = ctx.Get(Path) };
    // ...
}
```

Then in a workflow:

```csharp
new WebhookTrigger { Path = new("/hook"), CanStartWorkflow = true }
```

Studying built-in triggers (`HttpEndpoint`, `Timer`) is the fastest way to learn the `ITrigger` contract.

## Registration

```csharp
elsa.AddActivity<SendEmail>();

// Scan an assembly by a marker type:
elsa.AddActivitiesFrom<SomeMarkerType>();
```

## Connections (shared config)

Elsa 3.8+ supports **Connections**: named, reusable configuration shared between activities (e.g. a DB connection string, an HTTP client). See [Connections for Activities](https://docs.elsaworkflows.io/extensibility/connections.md). Use when multiple activities need the same external config and you want it editable in Studio.

## Studio metadata

For Studio to render your activity nicely:

- `[Activity(...)]` with category/display name.
- `[ActivityInput]`/`[ActivityOutput]` with `Hint`, `UIHint`, options providers.
- Icons via `ActivityIcon` or custom icon providers.

If you're code-first only (no Studio), these are optional. But adding them costs little and helps ops/inspection.

## Testing custom activities

See chapter 11. Key: resolve from DI, run via `IWorkflowRunner` with a tiny workflow wrapping the activity.

## Port providers (dynamic ports)

Activities can expose dynamic input/output ports based on configuration (e.g. a switch with N cases). See [Activity Port Providers](https://docs.elsaworkflows.io/extensibility/activity-port-providers.md). Advanced; skip until needed.

## Custom resilience

Wrap activity execution in Polly policies (retry, circuit breaker) via custom resilience strategies. See [Custom Resilience Strategies](https://docs.elsaworkflows.io/extensibility/custom-resilience-strategies.md).

## Tips

- Keep activities **single-purpose** and small. Compose them.
- Don't put orchestration logic in activities — that's the workflow's job.
- Make side-effecting activities **idempotent** (they may re-run after a crash).
- Validate inputs early and throw typed exceptions → becomes incidents with useful messages.
- Log via `ILogger<T>` injected into the activity, not `Console`.

## Next

Continue with [10 — Runtime APIs](10-runtime-apis.md).
