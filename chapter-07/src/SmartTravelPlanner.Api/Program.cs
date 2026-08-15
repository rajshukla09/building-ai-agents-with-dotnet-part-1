using Microsoft.AspNetCore.Diagnostics;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Tools;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Travelers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<SessionLifecycleOptions>()
    .Bind(builder.Configuration.GetSection(SessionLifecycleOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        settings => settings.ExpirationTimeoutMinutes > settings.IdleTimeoutMinutes,
        "ExpirationTimeoutMinutes must be greater than IdleTimeoutMinutes.")
    .ValidateOnStart();
builder.Services.AddSingleton<TravelAgent>();
builder.Services.AddSingleton<IExecutionTraceRecorder, ToolExecutionTraceRecorder>();
builder.Services.AddSingleton<WeatherTool>();
builder.Services.AddSingleton<CurrencyTool>();
builder.Services.AddSingleton<TimeZoneTool>();
builder.Services.AddSingleton<DistanceTool>();
builder.Services.AddSingleton<ITravelAgent>(services => services.GetRequiredService<TravelAgent>());
builder.Services.AddSingleton<IAgentSessionSerializer>(services => services.GetRequiredService<TravelAgent>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConversationStore, JsonConversationStore>();
builder.Services.AddSingleton<IConversationService, ConversationService>();
builder.Services.AddSingleton<ITravelerMemoryStore, TravelerMemoryStore>();
builder.Services.AddSingleton<TravelerMemoryService>();
builder.Services.AddSingleton<ITravelerStore, JsonTravelerStore>();
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
