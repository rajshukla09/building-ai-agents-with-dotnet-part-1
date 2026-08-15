using System.Collections.Concurrent;
using System.Text.Json;
using SmartTravelPlanner.Api.Models.Memory;

namespace SmartTravelPlanner.Api.Memory;

public sealed class TravelerMemoryStore : ITravelerMemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ConcurrentDictionary<Guid, TravelerMemory> _memories = new();
    private readonly object _fileLock = new();
    private readonly string _filePath;
    private readonly ILogger<TravelerMemoryStore> _logger;

    public TravelerMemoryStore(IWebHostEnvironment environment, ILogger<TravelerMemoryStore> logger)
        : this(Path.Combine(environment.ContentRootPath, "App_Data", "traveler-memories.json"), logger)
    {
    }

    public TravelerMemoryStore(string filePath, ILogger<TravelerMemoryStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
        Load();
    }

    public TravelerMemory? Get(Guid travelerId) => _memories.TryGetValue(travelerId, out var memory) ? memory : null;

    public TravelerMemory Upsert(TravelerMemory memory)
    {
        if (memory.TravelerId == Guid.Empty)
            throw new ArgumentException("TravelerId cannot be empty.", nameof(memory));
        _memories[memory.TravelerId] = memory;
        Flush();
        return memory;
    }

    public bool Delete(Guid travelerId)
    {
        bool removed = _memories.TryRemove(travelerId, out _);
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
            var document = JsonSerializer.Deserialize<TravelerMemoryDocument>(File.ReadAllText(_filePath), JsonOptions);
            foreach (var memory in document?.Travelers ?? [])
                if (memory.TravelerId != Guid.Empty)
                    _memories[memory.TravelerId] = memory;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Traveler memory file could not be loaded; starting with empty memory");
        }
    }

    private void Flush()
    {
        lock (_fileLock)
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                string temporaryPath = _filePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(
                                                     new TravelerMemoryDocument(
                                                         1, _memories.Values.OrderBy(x => x.TravelerId).ToArray()),
                                                     JsonOptions));
                File.Move(temporaryPath, _filePath, true);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Traveler memories could not be persisted to {PersistenceFile}", _filePath);
                throw;
            }
        }
    }

    private sealed record TravelerMemoryDocument(int Version, IReadOnlyCollection<TravelerMemory> Travelers);
}
