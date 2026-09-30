namespace Contracts.Wantlist;

public sealed record WantlistQueryOptions(bool IncludeRemoved = false)
{
	public static WantlistQueryOptions Default { get; } = new WantlistQueryOptions();

	public static WantlistQueryOptions WithRemoved { get; } = new WantlistQueryOptions(IncludeRemoved: true);
}
