using Contracts.Media;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

public class TitleRunTests
{
	private static readonly MediaIdentity Survivor = new(MediaId.New(), [TmdbId.From("1", MediaType.Movie)]);
	private static readonly MediaIdentity Absorbed = new(MediaId.New(), [ImdbId.From("tt1", MediaType.Movie)]);

	[Test]
	public async Task HandOut_ForAnAbsorbedId_HandsOutTheSurvivor()
	{
		await using var buffer = new MediaWriteBuffer(IMediaRepository.Mock().Object, NullLogger<MediaWriteBuffer>.Instance);
		var testee = Run(buffer);
		buffer.MergeIdentity(Absorbed.Id, Survivor with { Externals = Survivor.Externals + Absorbed.Externals });

		var handed = testee.HandOut(new MatchedMedia(Absorbed, new MovieDetails { Title = "Heat" }) { Known = true });

		await Assert
			.That(handed)
			.IsOfUnionType<MediaMatchResult, MatchedMedia>()
			.And.Multiple(async matched =>
			{
				await Assert.That(matched.Identity.Id).IsEqualTo(Survivor.Id);
				await Assert.That(matched.Known).IsTrue();
			});
	}

	[Test]
	public async Task HandOut_ForAnUnmatchedTitle_PassesItThrough()
	{
		await using var buffer = new MediaWriteBuffer(IMediaRepository.Mock().Object, NullLogger<MediaWriteBuffer>.Instance);
		var testee = Run(buffer);
		var unmatched = new UnmatchedMedia([TmdbId.From("2", MediaType.Movie)], new MovieDetails { Title = "Ronin" }, UnmatchedReason.NotFound);

		var handed = testee.HandOut(unmatched);

		await Assert.That(handed).IsEqualTo(unmatched);
	}

	private static TitleRun Run(MediaWriteBuffer buffer)
	{
		return new TitleRun(_ => Some.Of(MediaProvider.Tmdb), new HashSet<RemoteService>(), new HashSet<RemoteService>(), new IdentityMerge(), buffer);
	}
}
