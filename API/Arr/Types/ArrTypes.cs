namespace API.Arr.Types;

internal sealed record ArrTag(int Id, string Label);

internal sealed record ArrTitle(int Id, string? Title, int Year, int TmdbId, int TvdbId, string? ImdbId, IReadOnlyList<int> Tags);

internal sealed record ArrValidationFailure(string? PropertyName, string? ErrorMessage);
