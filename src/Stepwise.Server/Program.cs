using Elsa.Extensions;
using Stepwise.Server.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddElsa(elsa =>
{
    elsa.AddWorkflow<EchoWorkflow>();
    elsa.AddWorkflow<Test>();

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

app.UseWorkflows();
app.Run();
