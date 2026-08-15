namespace SmartTravelPlanner.Api.Classification;

public sealed class RequestClassificationException(string message) : InvalidOperationException(message);
