using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Context;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Hubs;
using SmartTravelPlanner.Api.Live;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Persistence;
using SmartTravelPlanner.Api.Routing;
using SmartTravelPlanner.Api.Tools;
using SmartTravelPlanner.Api.Travelers;
using SmartTravelPlanner.Api.Workflows;
using SmartTravelPlanner.Api.Workflows.Executors;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ToolExecutionOptions>()
    .Bind(builder.Configuration.GetSection(ToolExecutionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SessionLifecycleOptions>()
    .Bind(builder.Configuration.GetSection(SessionLifecycleOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(settings => settings.ExpirationTimeoutMinutes > settings.IdleTimeoutMinutes,
              "ExpirationTimeoutMinutes must be greater than IdleTimeoutMinutes.")
    .ValidateOnStart();
builder.Services.AddSingleton<TravelAgent>();
builder.Services.AddSingleton<IAgentFailurePolicy, DefaultAgentFailurePolicy>();
builder.Services.AddSingleton<TravelInvocationContextAccessor>();
builder.Services.AddSingleton<TravelerMemoryContextProvider>();
builder.Services.AddSingleton<RuntimeTravelContextProvider>();
builder.Services.AddSingleton<IExecutionTraceRecorder, ToolExecutionTraceRecorder>();
builder.Services.AddSingleton<WeatherTool>();
builder.Services.AddSingleton<CurrencyTool>();
builder.Services.AddSingleton<TimeZoneTool>();
builder.Services.AddSingleton<DistanceTool>();
builder.Services.AddSingleton<IRequestClassifier, RequestClassifier>();
builder.Services.AddSingleton<ExecutionPlanValidator>();
builder.Services.AddSingleton<IExecutionPlanRepairAgent, ExecutionPlanRepairAgent>();
builder.Services.AddSingleton<IExecutionPlanProvider, ExecutionPlanProvider>();
builder.Services.AddSingleton<IToolRouter, ToolRouter>();
builder.Services.AddSingleton<IToolExecutionPipeline, ToolExecutionPipeline>();
builder.Services.AddScoped<ExecutionPlanExecutor>();
builder.Services.AddScoped<ExecutionPlanValidationExecutor>();
builder.Services.AddScoped<ToolExecutionExecutor>();
builder.Services.AddScoped<TravelAgentExecutor>();
builder.Services.AddScoped<TravelPlanningWorkflow>();
builder.Services.AddSingleton<IWorkflowTraceRecorder, WorkflowTraceRecorder>();
builder.Services.AddScoped<ITravelWorkflowService, TravelWorkflowService>();
builder.Services.AddSingleton<IWorkflowExecutionQueue, WorkflowExecutionQueue>();
builder.Services.AddHostedService<WorkflowExecutionBackgroundService>();
builder.Services.AddSingleton<IWorkflowLiveEventPublisher, SignalRWorkflowLiveEventPublisher>();
builder.Services.AddSingleton<ITravelAgent>(services => services.GetRequiredService<TravelAgent>());
builder.Services.AddSingleton<IAgentSessionSerializer>(services => services.GetRequiredService<TravelAgent>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConversationStore, JsonConversationStore>();
builder.Services.AddSingleton<IConversationService, ConversationService>();
builder.Services.AddSingleton<ITravelerMemoryStore, TravelerMemoryStore>();
builder.Services.AddSingleton<TravelerMemoryService>();
builder.Services.AddSingleton<ITravelerStore, JsonTravelerStore>();
builder.Services.AddControllers().AddJsonOptions(
    options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.EnableAnnotations());
builder.Services.AddSignalR().AddJsonProtocol(
    options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOptions<WorkflowPersistenceOptions>()
    .Bind(builder.Configuration.GetSection(WorkflowPersistenceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddDbContextFactory<WorkflowDbContext>(
    options => options.UseSqlite(builder.Configuration.GetConnectionString("WorkflowRuns") ??
                                 "Data Source=workflow-runs.db"));
builder.Services.AddSingleton<IWorkflowRunStore, EfWorkflowRunStore>();

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(
    options => options.AddPolicy("BlazorClient",
                                 policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

WebApplication app = builder.Build();
using (IServiceScope scope = app.Services.CreateScope())
{
    await WorkflowDatabaseInitializer.EnsureReadyAsync(scope.ServiceProvider.GetRequiredService<WorkflowDbContext>());
}

app.UseExceptionHandler(
    errorApp =>
    {
        errorApp.Run(async context =>
                     {
                         IExceptionHandlerFeature? error = context.Features.Get<IExceptionHandlerFeature>();
                         ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(
                             "GlobalExceptionHandler");
                         logger.LogError(error?.Error, "An unhandled error occurred while processing the request");

                         bool classificationError = error?.Error is RequestClassificationException;
                         int statusCode = classificationError ? StatusCodes.Status422UnprocessableEntity
                                                              : StatusCodes.Status500InternalServerError;
                         context.Response.StatusCode = statusCode;
                         await Results
                             .Problem(statusCode: statusCode,
                                      title: classificationError ? "The request could not be classified"
                                                                 : "Unable to create a travel plan",
                                      detail: classificationError
                                          ? error!.Error.Message
                                          : "The travel-planning request could not be completed. Please try again.")
                             .ExecuteAsync(context);
                     });
    });

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("BlazorClient");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapControllers();
app.MapHub<WorkflowEventHub>("/hubs/workflow-events");

app.Run();

public partial class Program
{
}
