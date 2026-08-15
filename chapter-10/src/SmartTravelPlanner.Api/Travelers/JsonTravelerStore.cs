using System.Collections.Concurrent;
using System.Text.Json;

namespace SmartTravelPlanner.Api.Travelers;

public sealed class JsonTravelerStore : ITravelerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<Guid, TravelerProfile> _travelers = new();
    private readonly object _fileLock = new();
    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonTravelerStore> _logger;

    public JsonTravelerStore(IWebHostEnvironment environment, TimeProvider timeProvider, ILogger<JsonTravelerStore> logger)
        : this(Path.Combine(environment.ContentRootPath, "App_Data", "travelers.json"), timeProvider, logger) { }

    public JsonTravelerStore(string filePath, TimeProvider timeProvider, ILogger<JsonTravelerStore> logger)
    {
        _filePath = filePath;
        _timeProvider = timeProvider;
        _logger = logger;
        Load();
    }

    public TravelerProfile Add()
    {
        TravelerProfile profile;
        do
            profile = new TravelerProfile { TravelerId = Guid.NewGuid(), CreatedAt = _timeProvider.GetUtcNow() };
        while (!_travelers.TryAdd(profile.TravelerId, profile));
        Flush();
        return profile;
    }

    public TravelerProfile? Get(Guid travelerId) => _travelers.TryGetValue(travelerId, out var profile) ? profile : null;

    public bool Exists(Guid travelerId) => _travelers.ContainsKey(travelerId);

    public bool Delete(Guid travelerId)
    {
        bool removed = _travelers.TryRemove(travelerId, out _);
        if (removed)
            Flush();
        return removed;
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;
        try
        {
            TravelerDocument? document = JsonSerializer.Deserialize<TravelerDocument>(File.ReadAllText(_filePath), JsonOptions);
            foreach (TravelerProfile profile in document?.Travelers ?? [])
                if (profile.TravelerId != Guid.Empty && profile.CreatedAt != default)
                    _travelers[profile.TravelerId] = profile;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Traveler identity file could not be loaded; starting with an empty store");
        }
    }

    private void Flush()
    {
        lock (_fileLock)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(
                new TravelerDocument(1, _travelers.Values.OrderBy(x => x.CreatedAt).ToArray()), JsonOptions));
            File.Move(temporaryPath, _filePath, true);
        }
    }

    private sealed record TravelerDocument(int Version, IReadOnlyCollection<TravelerProfile> Travelers);
}
