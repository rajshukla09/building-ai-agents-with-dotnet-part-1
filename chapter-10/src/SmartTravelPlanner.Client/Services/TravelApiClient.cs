using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Client.Services;

public interface ITravelApiClient
{
    Task<ApiResult<TripPlanResponse>> CreatePlanAsync(TravelPlanRequest request,
                                                      CancellationToken cancellationToken = default);

    Task<ApiResult<StartWorkflowRunResponse>> StartWorkflowRunAsync(TravelPlanRequest request,
                                                                    CancellationToken cancellationToken = default);

    Task<ApiResult<WorkflowTopologyDto>> GetTopologyAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<IReadOnlyList<WorkflowLiveEventDto>>> GetEventsAsync(Guid workflowRunId, long afterSequence = 0,
                                                                        CancellationToken cancellationToken = default);

    Task<ApiResult<WorkflowRunDetailsDto>> GetRunDetailsAsync(Guid workflowRunId,
                                                              CancellationToken cancellationToken = default);

    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public enum ApiFailureKind
{
    ApiUnavailable,
    HttpErrorResponse,
    RequestTimeout,
    RequestCancelled,
    InvalidApiResponse,
    WorkflowFailure
}

public sealed record ApiFailure(ApiFailureKind Kind, int? StatusCode, string Message, string? JsonPath = null);
public sealed record ApiResult<T>(T? Value, ApiFailure? Failure)
{
    public bool IsSuccess => Failure is null && Value is not null;
}

public sealed class TravelApiClient(HttpClient httpClient) : ITravelApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<ApiResult<TripPlanResponse>> CreatePlanAsync(TravelPlanRequest request,
                                                             CancellationToken cancellationToken = default) =>
        SendAsync<TripPlanResponse>(() => httpClient.PostAsJsonAsync("api/travel/plan", request, JsonOptions,
                                                                     cancellationToken),
                                    cancellationToken);

    public Task<ApiResult<StartWorkflowRunResponse>>
    StartWorkflowRunAsync(TravelPlanRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<StartWorkflowRunResponse>(() => httpClient.PostAsJsonAsync("api/workflow-runs", request, JsonOptions,
                                                                             cancellationToken),
                                            cancellationToken);

    public Task<ApiResult<WorkflowTopologyDto>>
    GetTopologyAsync(CancellationToken cancellationToken = default) => SendAsync<WorkflowTopologyDto>(
        () => httpClient.GetAsync("api/workflows/travel-planning/topology", cancellationToken), cancellationToken);

    public Task<ApiResult<IReadOnlyList<WorkflowLiveEventDto>>>
    GetEventsAsync(Guid workflowRunId, long afterSequence = 0, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<WorkflowLiveEventDto>>(
            () => httpClient.GetAsync($"api/workflow-runs/{workflowRunId}/events?afterSequence={afterSequence}",
                                      cancellationToken),
            cancellationToken);

    public Task<ApiResult<WorkflowRunDetailsDto>> GetRunDetailsAsync(Guid workflowRunId,
                                                                     CancellationToken cancellationToken = default) =>
        SendAsync<WorkflowRunDetailsDto>(() => httpClient.GetAsync($"api/workflow-runs/{workflowRunId}",
                                                                   cancellationToken),
                                         cancellationToken);

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return false;
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send,
                                                  CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await send();
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(default, new(response.StatusCode == HttpStatusCode.UnprocessableEntity
                                            ? ApiFailureKind.WorkflowFailure
                                            : ApiFailureKind.HttpErrorResponse,
                                        (int)response.StatusCode, ReadProblem(body, response.ReasonPhrase)));
            try
            {
                T? value = JsonSerializer.Deserialize<T>(body, JsonOptions);
                return value is null
                           ? new(default,
                                 new(ApiFailureKind.InvalidApiResponse, (int)response.StatusCode,
                                     "The API responded successfully, but the response contract could not be read."))
                           : new(value, null);
            }
            catch (JsonException exception)
            {
                Console.Error.WriteLine(exception);
                return new(default, new(ApiFailureKind.InvalidApiResponse, (int)response.StatusCode,
                                        "The API responded successfully, but the response contract could not be read.",
                                        exception.Path));
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine(exception);
            return new(default, new(ApiFailureKind.RequestTimeout, null, "Request timeout."));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine(exception);
            return new(default, new(ApiFailureKind.RequestCancelled, null, "Request cancelled."));
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine(exception);
            return new(default,
                       new(ApiFailureKind.ApiUnavailable,
                           exception.StatusCode is null ? null : (int)exception.StatusCode.Value, "API unavailable."));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return new(default, new(ApiFailureKind.ApiUnavailable, null, exception.Message));
        }
    }

    private static string ReadProblem(string body, string? fallback)
    {
        try
        {
            ApiProblem? problem = JsonSerializer.Deserialize<ApiProblem>(body, JsonOptions);
            return problem?.Detail ?? problem?.Title ?? fallback ?? "API request failed.";
        }
        catch (JsonException)
        {
            return fallback ?? "API request failed.";
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
