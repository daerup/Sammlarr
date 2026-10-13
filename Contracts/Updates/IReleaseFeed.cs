namespace Contracts.Updates;

public sealed record PublishedRelease(string Tag, Uri Page);

public interface IReleaseFeed
{
	Task<Optional<PublishedRelease>> LatestAsync(string repository, CancellationToken cancellationToken = default);
}
