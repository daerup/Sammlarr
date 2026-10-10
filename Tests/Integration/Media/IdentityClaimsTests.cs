using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class IdentityClaimsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IIdentityClaims Testee => host.Resolve<IIdentityClaims>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task ClaimAsync_ForTwoRemakesClaimedTogether_StoresTwoRowsUnderTheIdsItHandsOut()
	{
		var outcomes = await InOneGroupAsync(Claim([Tmdb("15739"), Imdb("tt0083564")]), Claim([Tmdb("26177"), Imdb("tt0207972")]));

		var first = await Assert.That(outcomes[0]).IsOfUnionType<ClaimOutcome, Claimed>();
		var second = await Assert.That(outcomes[1]).IsOfUnionType<ClaimOutcome, Claimed>();
		List<Guid> ids = [first!.Id.Value, second!.Id.Value];
		host.ScopedDbContext.ChangeTracker.Clear();
		await Assert.That(await host.ScopedDbContext.Media.CountAsync(media => ids.Contains(media.Id))).IsEqualTo(2);
	}

	[Test]
	public async Task ClaimAsync_ForOverlappingClaimsInOneGroup_PutsThemOnOneRow()
	{
		var outcomes = await InOneGroupAsync(Claim([Tmdb("15739")]), Claim([Tmdb("15739"), Imdb("tt0083564")]));

		var first = await Assert.That(outcomes[0]).IsOfUnionType<ClaimOutcome, Claimed>();
		var second = await Assert.That(outcomes[1]).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(second!.Id).IsEqualTo(first!.Id);
		await Assert.That(second.Keys.Select(key => $"{key}")).IsEquivalentTo(["Tmdb:Movie:15739", "Imdb:Movie:tt0083564"]);
	}

	private async Task<ClaimOutcome[]> InOneGroupAsync(params IdentityClaim[] claims)
	{
		var held = await host.Resolve<MediaWriteLock>().AcquireAsync(CancellationToken.None);
		var occupying = Testee.ClaimAsync(Claim([Tmdb("999999")]));
		List<Task<ClaimOutcome>> grouped = [.. claims.Select(claim => Testee.ClaimAsync(claim))];
		held.Dispose();
		await occupying;
		return await Task.WhenAll(grouped);
	}

	[Test]
	public async Task ClaimAsync_ForKeysOnTwoRows_ConflictsWithBothRowsAndAddsNothing()
	{
		var first = await host.SeedMediaAsync(Tmdb("15739"));
		var second = await host.SeedMediaAsync(Imdb("tt0207972"));

		var outcome = await Testee.ClaimAsync(Claim([Tmdb("15739"), Imdb("tt0207972"), Trakt("9600")]));

		var conflicted = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Conflicted>();
		await Assert.That(conflicted!.Owners).IsEquivalentTo([first, second]);
		host.ScopedDbContext.ChangeTracker.Clear();
		await Assert.That(await host.ScopedDbContext.ExternalMediaIds.CountAsync()).IsEqualTo(2);
	}

	[Test]
	public async Task ClaimAsync_ForAPinnedTitleWhoseEnricherNamesAnotherRowsKey_KeepsTheIdAndDropsThatKey()
	{
		var annie = await host.SeedMediaAsync(Tmdb("15739"));
		var remake = await host.SeedMediaAsync(Imdb("tt0207972"));

		var outcome = await Testee.ClaimAsync(new IdentityClaim([Imdb("tt0207972"), Trakt("9600")], Some.Of(annie), [RemoteService.Tmdb]));

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		host.ScopedDbContext.ChangeTracker.Clear();
		var owners = await host.ScopedDbContext.ExternalMediaIds.Select(external => new { external.Id, Owner = external.Media.Id }).ToListAsync();
		await Assert.That(claimed!.Id).IsEqualTo(annie);
		await Assert.That(owners.Single(owner => owner.Id == "tt0207972").Owner).IsEqualTo(remake.Value);
		await Assert.That(owners.Single(owner => owner.Id == "9600").Owner).IsEqualTo(annie.Value);
	}

	[Test]
	public async Task ClaimAsync_WhenTheLibraryRejectsOneClaimOfFifty_StoresTheOtherFortyNine()
	{
		await host.Context.Database.ExecuteSqlRawAsync(
			"""CREATE TRIGGER "RejectPoison" BEFORE INSERT ON "ExternalMediaIds" WHEN NEW."Id" = 'poison' BEGIN SELECT RAISE(ABORT, 'poison'); END;"""
		);

		var outcomes = await Task.WhenAll(Enumerable.Range(0, 50).Select(index => Testee.ClaimAsync(Claim([Tmdb(index == 25 ? "poison" : $"{1000 + index}")]))));

		host.ScopedDbContext.ChangeTracker.Clear();
		await Assert.That(outcomes.Count(outcome => outcome is Conflicted)).IsEqualTo(1);
		await Assert.That(await host.ScopedDbContext.ExternalMediaIds.CountAsync()).IsEqualTo(49);
		await Assert.That(await host.ScopedDbContext.Media.CountAsync()).IsEqualTo(49);
	}

	[Test]
	public async Task ClaimAsync_ForAStoredRowsKeyAndANewOne_AddsTheNewKeyToThatRow()
	{
		var annie = await host.SeedMediaAsync(Tmdb("15739"));

		var outcome = await Testee.ClaimAsync(Claim([Tmdb("15739"), Imdb("tt0083564")]));

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(claimed!.Id).IsEqualTo(annie);
		var stored = await host.Resolve<IMediaRepository>().GetAsync([annie]);
		await Assert.That(stored.Single().Identity.Keys.Select(key => $"{key}")).IsEquivalentTo(["Tmdb:Movie:15739", "Imdb:Movie:tt0083564"]);
	}

	[Test]
	public async Task Migration_CreatesOneKeyPerSourceIndex()
	{
		await host.SeedMediaAsync(Tmdb("15739"));
		var media = await host.Context.Media.SingleAsync();
		host.Context.ExternalMediaIds.Add(Tmdb("26177").ToExternalIdEntity(media));

		await Assert.That(async () => await host.Context.SaveChangesAsync()).Throws<DbUpdateException>();
	}

	private static IdentityClaim Claim(ValueSet<IIdentityKey> keys) => new(keys, new None(), [RemoteService.Mdblist]);

	private static IIdentityKey Tmdb(string value) => TmdbId.From(value, MediaType.Movie);

	private static IIdentityKey Imdb(string value) => ImdbId.From(value, MediaType.Movie);

	private static IIdentityKey Trakt(string value) => TraktId.From(value, MediaType.Movie);
}
