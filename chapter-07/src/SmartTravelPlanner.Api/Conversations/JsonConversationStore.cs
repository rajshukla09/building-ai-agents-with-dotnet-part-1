using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class JsonConversationStore : IConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<Guid, ConversationState> _conversations = new();
    private readonly ConcurrentDictionary<Guid, ConversationDocument> _documents = new();
    private readonly object _fileLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly SessionLifecycleOptions _options;
    private readonly IAgentSessionSerializer _sessionSerializer;
    private readonly ILogger<JsonConversationStore> _logger;
    private readonly string _filePath;

    public JsonConversationStore(
        TimeProvider timeProvider,
        IOptions<SessionLifecycleOptions> options,
        IAgentSessionSerializer sessionSerializer,
        IWebHostEnvironment environment,
        ILogger<JsonConversationStore> logger)
        : this(timeProvider, options, sessionSerializer, logger,
            Path.Combine(environment.ContentRootPath, "App_Data", "conversations.json"))
    {
    }

    public JsonConversationStore(
        TimeProvider timeProvider,
        IOptions<SessionLifecycleOptions> options,
        IAgentSessionSerializer sessionSerializer,
        ILogger<JsonConversationStore> logger,
        string filePath)
    {
        _timeProvider = timeProvider;
        _options = options.Value;
        _sessionSerializer = sessionSerializer;
        _logger = logger;
        _filePath = filePath;
        Load();
    }

    public ConversationState Add(AgentSession session) => Add(session, Guid.NewGuid());

    public ConversationState Add(AgentSession session, Guid travelerId)
    {
        ConversationState conversation;
        do
        {
            conversation = new ConversationState(Guid.NewGuid(), travelerId, session, _timeProvider.GetUtcNow(), _options.ExpirationTimeout);
        }
        while (!_conversations.TryAdd(conversation.Id, conversation));

        Capture(conversation);
        Flush();
        return conversation;
    }

    public bool TryGet(Guid conversationId, out ConversationState conversation) =>
        _conversations.TryGetValue(conversationId, out conversation!);

    public IReadOnlyCollection<ConversationState> GetAll() => _conversations.Values.ToArray();

    public void Update(ConversationState conversation)
    {
        if (_conversations.ContainsKey(conversation.Id))
        {
            Capture(conversation);
            Flush();
        }
    }

    public bool Delete(Guid conversationId)
    {
        bool deleted = _conversations.TryRemove(conversationId, out _);
        if (deleted)
        {
            _documents.TryRemove(conversationId, out _);
            Flush();
        }

        return deleted;
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            using JsonDocument root = JsonDocument.Parse(json);
            if (!root.RootElement.TryGetProperty("conversations", out JsonElement conversations) ||
                conversations.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement element in conversations.EnumerateArray())
            {
                try
                {
                    ConversationDocument? document = element.Deserialize<ConversationDocument>(JsonOptions);
                    if (document is null || document.Id == Guid.Empty || document.MessageCount < 0 ||
                        !Enum.IsDefined(document.Status) || document.Status == SessionStatus.Removed ||
                        document.CreatedAt == default || document.LastActivityAt < document.CreatedAt ||
                        document.ExpirationTime < document.LastActivityAt)
                    {
                        continue;
                    }

                    AgentSession session = _sessionSerializer.DeserializeSession(document.AgentSession);
                    ConversationState state = new(document.Id, document.TravelerId == Guid.Empty ? Guid.NewGuid() : document.TravelerId,
                        session, document.CreatedAt, document.LastActivityAt,
                        document.ExpirationTime, document.MessageCount, document.Status);
                    if (!_conversations.TryAdd(document.Id, state))
                    {
                        _logger.LogWarning("Ignored duplicate persisted conversation {ConversationId}", document.Id);
                    }
                    else
                    {
                        _documents[document.Id] = document;
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Ignored a conversation that could not be restored");
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Conversation persistence file could not be loaded; starting with an empty store");
        }
    }

    private void Flush()
    {
        lock (_fileLock)
        {
            try
            {
                ConversationDocument[] documents = _documents.Values
                    .OrderBy(document => document.CreatedAt)
                    .ToArray();
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporaryPath = _filePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new ConversationStoreDocument(1, documents), JsonOptions));
                File.Move(temporaryPath, _filePath, true);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Conversations could not be persisted to {PersistenceFile}", _filePath);
            }
        }
    }

    private ConversationDocument ToDocument(ConversationState conversation)
    {
        ConversationMetadata metadata = conversation.ToMetadata(_timeProvider.GetUtcNow(), _options);
        return new ConversationDocument(metadata.ConversationId, metadata.TravelerId, _sessionSerializer.SerializeSession(conversation.Session),
            metadata.Status, metadata.CreatedAt, metadata.LastActivityAt, metadata.ExpirationTime, metadata.MessageCount);
    }

    private void Capture(ConversationState conversation)
    {
        try
        {
            _documents[conversation.Id] = ToDocument(conversation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Conversation {ConversationId} could not be prepared for persistence", conversation.Id);
        }
    }
}
