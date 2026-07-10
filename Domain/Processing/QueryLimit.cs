namespace Domain.Processing;

public record QueryLimit
{
	public required int Count { get; init; }
	public SlotPolicy Watchlisted { get; init; } = SlotPolicy.Consumed;
	public SlotPolicy Blacklisted { get; init; } = SlotPolicy.Consumed;
}
