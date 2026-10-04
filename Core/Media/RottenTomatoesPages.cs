using Contracts;
using Contracts.Media;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class RottenTomatoesPages(
	IMediaRepository media,
	IRottenTomatoesPageFinder finder,
	IDateTimeProvider clock,
	ILogger<RottenTomatoesPages> logger
) : IRottenTomatoesPages
{
	public async Task<Optional<string>> FindPathAsync(MediaId id, CancellationToken cancellationToken = default)
	{
		var stored = await media.FindRottenTomatoesAnswerAsync(id, cancellationToken);
		return await stored.Match(answer => Task.FromResult(answer.Path), () => AskAsync(id, cancellationToken));
	}

	private async Task<Optional<string>> AskAsync(MediaId id, CancellationToken cancellationToken)
	{
		var externals = await media.GetExternalsAsync([id], MediaProvider.Imdb, cancellationToken);
		return await externals
			.GetValueOrNone(id)
			.Match(imdb => LookUpAsync(id, ImdbId.From(imdb.Value, imdb.Type), cancellationToken), () => Task.FromResult<Optional<string>>(new None()));
	}

	private async Task<Optional<string>> LookUpAsync(MediaId id, ImdbId imdb, CancellationToken cancellationToken)
	{
		var found = await finder.FindPathAsync(imdb, cancellationToken);
		found.ApplyError(error => logger.LogWarning("Rotten Tomatoes page lookup for {ImdbId} failed: {Reason}", imdb.Value, error.Reason));
		await found.Match(path => media.StoreRottenTomatoesAnswerAsync(id, path, clock.Now, cancellationToken), _ => Task.CompletedTask);
		return found.GetValueOr(new None());
	}
}
