namespace Contracts.Media.List;

public record ImportList
{
	public required string Name { get; init; }
	public required Guid Id { get; init; }
	public required bool Enabled { get; init; }
	public required MediaListProvider Provider { get; init; }
	public required MediaListAddStrategy Strategy { get; init; }
	public required string ListIdentifier { get; init; }
	public required DateTimeOffset CreatedAt { get; init; }
}
