namespace Stepwise.Server.Workflows;

using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

internal class EchoWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new Input<string>("/echo"),
                    CanStartWorkflow = true,
                },
                new WriteLine("Hello!"),
            },
        };
    }
}