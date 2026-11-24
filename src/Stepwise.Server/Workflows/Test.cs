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
                new WriteHttpResponse
                {
                    Content = new Input<object?>(context => new
                    {
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