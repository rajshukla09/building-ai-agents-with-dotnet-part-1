using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    [Required(AllowEmptyStrings = false)]
    [Url]
    public string Endpoint { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string DeploymentName { get; init; } = string.Empty;
}
