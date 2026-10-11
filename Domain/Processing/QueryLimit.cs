namespace Domain.Processing;

public record QueryLimit
{
	public required int Count { get; init; }
	public SlotPolicy Wantlisted { get; init; } = SlotPolicy.Consumed;
	public SlotPolicy Blocklisted { get; init; } = SlotPolicy.Consumed;
	public LimitPick Pick { get; init; } = LimitPick.First;
}
