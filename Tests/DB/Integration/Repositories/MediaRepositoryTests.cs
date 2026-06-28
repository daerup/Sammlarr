using Contracts;
using Contracts.Media;
using DB.Data.Media;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IMediaRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
		_testee = new MediaRepository(fixture.ContextFactory, new MediaMetadataFactory(dateTimeProvider.Object));
	}

	[Test]
	public async Task FindManyAsync_WhenMediaExists_ReturnsMediaIdForSlot()
	{
		var imdbId = ImdbId.From("tt0101", MediaType.Movie);
		IExternalMediaId[] seededIds = [TmdbId.From("101", MediaType.Movie), imdbId, TvdbId.From("201", MediaType.Movie)];

		var seededMediaId = await SetUpMediaAsync(seededIds);

		var identities = await _testee.FindIdentitiesAsync([
			[imdbId],
		]);

		await Assert.That(identities.Count).IsEqualTo(1);
		await Assert.That(identities[0]!.Value.Id).IsEqualTo(seededMediaId);
	}

	[Test]
	public async Task FindManyAsync_WhenMultipleCandidatesShareMedia_ReturnsSingleMediaId()
	{
		var imdbId = ImdbId.From("tt0102", MediaType.Movie);
		var tmdbId = TmdbId.From("102", MediaType.Movie);
		IExternalMediaId[] seededIds = [tmdbId, imdbId, TvdbId.From("202", MediaType.Movie)];

		var seededMediaId = await SetUpMediaAsync(seededIds);

		var identities = await _testee.FindIdentitiesAsync([
			[imdbId, tmdbId],
		]);

		await Assert.That(identities[0]!.Value.Id).IsEqualTo(seededMediaId);
	}

	[Test]
	public async Task FindManyAsync_WhenNoIdsMatch_ReturnsNullSlot()
	{
		var identities = await _testee.FindIdentitiesAsync([
			[ImdbId.From("tt-not-found", MediaType.Movie)],
		]);

		await Assert.That(identities[0]).IsNull();
	}

	[Test]
	public async Task FindManyAsync_WhenCandidatesBridgeDisjointMediaRows_Throws()
	{
		var tmdbId = TmdbId.From("103", MediaType.Movie);
		var imdbId = ImdbId.From("tt0103", MediaType.Movie);
		await SetUpMediaAsync([tmdbId]);
		await SetUpMediaAsync([imdbId]);

		await Assert
			.That(async () =>
				await _testee.FindIdentitiesAsync([
					[tmdbId, imdbId],
				])
			)
			.Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task FindManyAsync_WhenEmptyInput_ReturnsEmptyList()
	{
		var identities = await _testee.FindIdentitiesAsync(Array.Empty<IReadOnlyList<IExternalMediaId>>());

		await Assert.That(identities).IsEmpty();
	}

	[Test]
	public async Task FindManyAsync_WhenMultipleGroups_ReturnsPositionalResults()
	{
		var firstImdb = ImdbId.From("tt0104", MediaType.Movie);
		var secondImdb = ImdbId.From("tt0105", MediaType.Movie);
		var firstMediaId = await SetUpMediaAsync([firstImdb]);
		var secondMediaId = await SetUpMediaAsync([secondImdb]);

		var identities = await _testee.FindIdentitiesAsync([
			[firstImdb],
			[ImdbId.From("tt-missing", MediaType.Movie)],
			[secondImdb],
		]);

		await Assert.That(identities.Count).IsEqualTo(3);
		await Assert.That(identities[0]!.Value.Id).IsEqualTo(firstMediaId);
		await Assert.That(identities[1]).IsNull();
		await Assert.That(identities[2]!.Value.Id).IsEqualTo(secondMediaId);
	}

	[Test]
	public async Task GetExternalsAsync_WhenMediaExists_ReturnsExternalForRequestedSource()
	{
		var ids = new IExternalMediaId[] { TmdbId.From("210", MediaType.Movie), PlexId.From("plex-210", MediaType.Movie) };
		var mediaId = await SetUpMediaAsync(ids);

		var result = await _testee.GetExternalsAsync([mediaId], MediaProvider.Plex);

		await Assert.That(result.Count).IsEqualTo(1);
		await Assert.That(result[mediaId]).IsEqualTo<IExternalMediaId>(PlexId.From("plex-210", MediaType.Movie));
	}

	[Test]
	public async Task UpsertManyAsync_WhenAllGroupsAreNew_CreatesOneRowPerGroupInSingleSave()
	{
		IReadOnlyList<MediaIdentity> identitiesInput =
		[
			new(MediaId.From(Guid.NewGuid()), [TmdbId.From("301", MediaType.Movie), ImdbId.From("tt0301", MediaType.Movie)]),
			new(MediaId.From(Guid.NewGuid()), [TmdbId.From("302", MediaType.Movie), ImdbId.From("tt0302", MediaType.Movie)]),
			new(MediaId.From(Guid.NewGuid()), [TmdbId.From("303", MediaType.Movie)]),
		];

		var identities = await _testee.UpsertIdentitiesAsync(identitiesInput);

		await Assert.That(identities.Count).IsEqualTo(3);
		await Assert.That(identities.Select(i => i.Id)).IsEquivalentTo(identitiesInput.Select(i => i.Id));
		var persisted = await fixture.Context.Media.Include(m => m.ExternalIds).ToListAsync();
		await Assert.That(persisted.Count).IsEqualTo(3);
	}

	[Test]
	public async Task UpsertManyAsync_WhenGroupMatchesExisting_AppendsMissingIds()
	{
		var tmdbId = TmdbId.From("310", MediaType.Movie);
		var existingId = await SetUpMediaAsync([tmdbId]);

		var newImdb = ImdbId.From("tt0310", MediaType.Movie);
		var identities = await _testee.UpsertIdentitiesAsync([new MediaIdentity(existingId, [tmdbId, newImdb])]);

		await Assert.That(identities.Count).IsEqualTo(1);
		await Assert.That(identities[0].Id).IsEqualTo(existingId);
		var media = await fixture.Context.Media.Include(m => m.ExternalIds).SingleAsync();
		await Assert.That(media.ExternalIds.Count).IsEqualTo(2);
	}

	[Test]
	public async Task UpsertManyAsync_WhenExternalAlreadyOwned_RoutesToExistingMedia()
	{
		var tmdbId = TmdbId.From("312", MediaType.Movie);
		var existingId = await SetUpMediaAsync([tmdbId]);

		var incoming = new MediaIdentity(MediaId.From(Guid.NewGuid()), [tmdbId, ImdbId.From("tt0312", MediaType.Movie)]);
		var identities = await _testee.UpsertIdentitiesAsync([incoming]);

		await Assert.That(identities[0].Id).IsEqualTo(existingId);
		var persisted = await fixture.Context.Media.Include(m => m.ExternalIds).ToListAsync();
		await Assert.That(persisted.Count).IsEqualTo(1);
		await Assert.That(persisted[0].ExternalIds.Count).IsEqualTo(2);
	}

	[Test]
	public async Task UpsertManyAsync_WhenTwoNewIdentitiesShareExternal_CreatesSingleMedia()
	{
		var shared = ImdbId.From("tt0320", MediaType.Movie);
		IReadOnlyList<MediaIdentity> input =
		[
			new(MediaId.From(Guid.NewGuid()), [TmdbId.From("320", MediaType.Movie), shared]),
			new(MediaId.From(Guid.NewGuid()), [shared, TvdbId.From("420", MediaType.Movie)]),
		];

		var identities = await _testee.UpsertIdentitiesAsync(input);

		await Assert.That(identities[0].Id).IsEqualTo(identities[1].Id);
		var persisted = await fixture.Context.Media.Include(m => m.ExternalIds).ToListAsync();
		await Assert.That(persisted.Count).IsEqualTo(1);
		await Assert.That(persisted[0].ExternalIds.Count).IsEqualTo(3);
	}

	[Test]
	public async Task UpsertManyAsync_WhenGroupBridgesDisjointMedia_Throws()
	{
		var tmdbId = TmdbId.From("311", MediaType.Movie);
		var imdbId = ImdbId.From("tt0311", MediaType.Movie);
		await SetUpMediaAsync([tmdbId]);
		await SetUpMediaAsync([imdbId]);

		await Assert
			.That(async () => await _testee.UpsertIdentitiesAsync([new MediaIdentity(MediaId.From(Guid.NewGuid()), [tmdbId, imdbId])]))
			.Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task UpsertManyAsync_WhenEmptyInput_ReturnsEmptyList()
	{
		var identities = await _testee.UpsertIdentitiesAsync(Array.Empty<MediaIdentity>());

		await Assert.That(identities).IsEmpty();
	}

	[Test]
	public async Task GetByExternalsAsync_ReturnsMatchingMedia()
	{
		var imdbId = ImdbId.From("tt0401", MediaType.Movie);
		var seededMediaId = await SetUpMediaAsync([imdbId]);

		var rows = await _testee.GetByExternalsAsync([imdbId]);

		await Assert.That(rows.Count).IsEqualTo(1);
		await Assert.That(rows[0].Stored.Identity.Id).IsEqualTo(seededMediaId);
	}

	[Test]
	public async Task GetAsync_ReturnsMediaForExistingRows()
	{
		var imdbId = ImdbId.From("tt0402", MediaType.Movie);
		var seededMediaId = await SetUpMediaAsync([imdbId]);

		var rows = await _testee.GetAsync([seededMediaId]);

		await Assert.That(rows.Count).IsEqualTo(1);
		await Assert.That(rows[0].Stored.Identity.Id).IsEqualTo(seededMediaId);
	}

	private async Task<MediaId> SetUpMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private static async Task AssertContainsIds(IEnumerable<IExternalMediaId> actualIds, IEnumerable<IExternalMediaId> expectedIds)
	{
		var actualIdList = actualIds.ToList();
		var expectedIdList = expectedIds.ToList();
		using var disposable = Assert.Multiple();
		await Assert.That(actualIdList).Count().IsEqualTo(expectedIdList.Count);
		foreach (var expectedId in expectedIdList)
		{
			await Assert.That(actualIdList).Contains(expectedId);
		}
	}
}
