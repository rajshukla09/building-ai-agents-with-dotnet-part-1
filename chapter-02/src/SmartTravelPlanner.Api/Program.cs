using Microsoft.AspNetCore.Diagnostics;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ITravelAgent, TravelAgent>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.EnableAnnotations());
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        IExceptionHandlerFeature? error = context.Features.Get<IExceptionHandlerFeature>();
        ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogError(error?.Error, "An unhandled error occurred while processing the request");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unable to create a travel plan",
            detail: "The travel-planning request could not be completed. Please try again.")
            .ExecuteAsync(context);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program
{
}
