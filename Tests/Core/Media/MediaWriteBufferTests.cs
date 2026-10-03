using Contracts.Media;
using Core.Media;
using Domain.Media;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Media;

public class MediaWriteBufferTests
{
	private static readonly DateTimeOffset FetchedAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
	private static readonly MediaIdentity Survivor = new(MediaId.From(Guid.NewGuid()), [TmdbId.From("1", MediaType.Movie)]);
	private static readonly MediaIdentity Absorbed = new(MediaId.From(Guid.NewGuid()), [ImdbId.From("tt1", MediaType.Movie)]);

	private readonly Mock<IMediaRepository> _repository = Repository();

	[Test]
	public async Task DisposeAsync_WhenTwoTitlesWithDetailsMergeIntoOne_FlushesOneDetailsRowForTheSurvivor()
	{
		await using (var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance))
		{
			testee.Add(new MatchedMedia(Survivor, new MovieDetails { Title = "Survivor" }), Cached("Survivor"));
			testee.Add(new MatchedMedia(Absorbed, new MovieDetails { Title = "Absorbed" }), Cached("Absorbed"));
			testee.MergeIdentity(Absorbed.Id, Survivor with { Externals = Survivor.Externals + Absorbed.Externals });
		}

		_repository
			.UpsertDetailsAsync(Arg.Is<IReadOnlyDictionary<MediaId, CachedDetails>>(details => details is { Count: 1 } && details.ContainsKey(Survivor.Id)), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task DisposeAsync_WhenTheLookupReportedMoreIds_PersistsThemWithTheIdentity()
	{
		var plexOnly = new MediaIdentity(MediaId.From(Guid.NewGuid()), [PlexId.From("plex-1", MediaType.Movie)]);
		var learned = TmdbId.From("27205", MediaType.Movie);

		await using (var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance))
		{
			testee.Add(new MatchedMedia(plexOnly with { Externals = plexOnly.Externals + [learned] }, new MovieDetails()), Cached("Inception"));
		}

		_repository
			.UpsertIdentitiesAsync(Arg.Is<IReadOnlyList<MediaIdentity>>(identities => identities is [var only] && only.Externals.Contains(learned)), Any())
			.WasCalled(Times.Once);
	}

	private static CachedDetails Cached(string title) => new(new MovieDetails { Title = title }, FetchedAt, MediaField.None);

	private static Mock<IMediaRepository> Repository()
	{
		var repository = IMediaRepository.Mock();
		repository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		return repository;
	}
}
