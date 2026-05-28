namespace Contracts.Feed;

public sealed class FeedIdentifierNotFoundException(string identifier) : Exception($"No list found with identifier '{identifier}'.")
{
	public string Identifier { get; } = identifier;
}
