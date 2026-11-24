namespace Stepwise.Server.Workflows;

using Elsa.Extensions;
using Elsa.Http;
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

internal class Test : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new Input<string>("/test"),
                    CanStartWorkflow = true,
                },
                // Use the correlation ID supplied by the caller (via ?correlationId=... or the
                // X-Correlation-ID header), or generate one so this instance can still be targeted.
                new Correlate(context =>
                {
                    var workflowContext = context.GetWorkflowExecutionContext();
                    return string.IsNullOrWhiteSpace(workflowContext.CorrelationId)
                        ? Guid.NewGuid().ToString("N")
                        : workflowContext.CorrelationId;
                }),
                new WriteHttpResponse
                {
                    Content = new Input<object?>(context => new
                    {
                        correlationId = context.GetWorkflowExecutionContext().CorrelationId,
                    }),
                },
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAny,
                    Branches =
                    {
                        new Sequence
                        {
                            Activities =
                            {
                                new HttpEndpoint
                                {
                                    Path = new Input<string>("/finish"),
                                    // Call with ?correlationId=... (or X-Correlation-ID) to resume
                                    // only the instance that was started with that correlation ID.
                                    CanStartWorkflow = false,
                                },                                
                                new WriteLine("OK."),
                            },
                        },
                        new Sequence
                        {
                            Activities =
                            {
                                new Delay(TimeSpan.FromSeconds(60)),
                                new WriteLine("Timeout."),
                            },
                        },
                    }
                },
                new WriteLine("Completed."),
            },
        };
    }
}