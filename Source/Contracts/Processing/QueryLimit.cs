namespace Contracts.Processing;

public record QueryLimit
{
	public required int Count { get; init; }
	public LimitBehavior Watchlisted { get; init; } = LimitBehavior.Included;
	public LimitBehavior Blacklisted { get; init; } = LimitBehavior.Included;
}
