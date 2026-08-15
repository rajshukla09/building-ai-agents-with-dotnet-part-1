using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class ConversationState
{
    private readonly object _lifecycleLock = new();
    private int _messageCount;
    private SessionStatus _status = SessionStatus.Created;

    public ConversationState(
        Guid id,
        AgentSession session,
        DateTimeOffset createdAt,
        TimeSpan expirationTimeout)
    {
        Id = id;
        Session = session;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
        ExpirationTime = createdAt.Add(expirationTimeout);
    }

    public Guid Id { get; }

    public AgentSession Session { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset ExpirationTime { get; private set; }

    public SemaphoreSlim TurnLock { get; } = new(1, 1);

    public SessionStatus RefreshStatus(DateTimeOffset now, SessionLifecycleOptions options)
    {
        lock (_lifecycleLock)
        {
            if (_status is SessionStatus.Expired or SessionStatus.Removed)
            {
                return _status;
            }

            if (now >= ExpirationTime)
            {
                _status = SessionStatus.Expired;
            }
            else if (_messageCount > 0 && now - LastActivityAt >= options.IdleTimeout)
            {
                _status = SessionStatus.Idle;
            }

            return _status;
        }
    }

    public void RecordMessage(DateTimeOffset timestamp, SessionLifecycleOptions options)
    {
        lock (_lifecycleLock)
        {
            _messageCount++;
            LastActivityAt = timestamp;
            ExpirationTime = timestamp.Add(options.ExpirationTimeout);
            _status = SessionStatus.Active;
        }
    }

    public void Expire(DateTimeOffset timestamp)
    {
        lock (_lifecycleLock)
        {
            _status = SessionStatus.Expired;
            ExpirationTime = timestamp;
        }
    }

    public void MarkRemoved()
    {
        lock (_lifecycleLock)
        {
            _status = SessionStatus.Removed;
        }
    }

    public ConversationMetadata ToMetadata(DateTimeOffset now, SessionLifecycleOptions options)
    {
        lock (_lifecycleLock)
        {
            RefreshStatus(now, options);
            return new(Id, CreatedAt, LastActivityAt, ExpirationTime, _messageCount, _status);
        }
    }
}
