using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Context;

public sealed record TravelInvocationContext(
    Guid? TravelerId = null,
    Guid? ConversationId = null,
    SessionStatus? SessionStatus = null,
    string? Destination = null,
    int? DurationDays = null);

public sealed class TravelInvocationContextAccessor
{
    private readonly AsyncLocal<TravelInvocationContext?> _current = new();

    public TravelInvocationContext? Current => _current.Value;

    public IDisposable Push(TravelInvocationContext context)
    {
        TravelInvocationContext? previous = _current.Value;
        _current.Value = context;
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
