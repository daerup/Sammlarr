namespace Domain.Providers;

public readonly record struct LanePace(DateTimeOffset OpensAt, int Requests, TimeSpan Per);
