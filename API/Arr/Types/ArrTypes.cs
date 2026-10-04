namespace API.Arr.Types;

internal sealed record ArrTag(int Id, string Label);

internal sealed record ArrTitle(int Id, string? Title, int Year, int TmdbId, int TvdbId, string? ImdbId, IReadOnlyList<int> Tags);

internal sealed record ArrValidationFailure(string? PropertyName, string? ErrorMessage);

internal sealed record ArrSystemStatus(string? AppName, string? Version);

internal sealed record ArrProfile(int Id, string? Name);

internal sealed record ArrFolder(string? Path, long FreeSpace);
