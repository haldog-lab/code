using System.Text.Json.Serialization;

namespace Shelf.Core;

public sealed record ShelfItemsState
{
    [JsonRequired] public int Version { get; init; } = 1;
    [JsonRequired] public List<string> Paths { get; init; } = [];

    public static bool IsValid(ShelfItemsState state) => state.Version == 1
        && state.Paths is not null
        && state.Paths.All(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path));
}

public sealed record ShelfSettings
{
    [JsonRequired] public int Version { get; init; } = 1;
    public string? MonitorId { get; init; }
    [JsonRequired] public bool IsCollapsed { get; init; }

    public static bool IsValid(ShelfSettings state) => state.Version == 1;
}
