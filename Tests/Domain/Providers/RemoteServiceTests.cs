using Domain.Providers;
using Domain.Wantlist.Arr;
using TestInfrastructure;

namespace Domain.Tests.Providers;

public class RemoteServiceTests
{
	public static IEnumerable<RemoteService> Remotes() => Enum.GetValues<RemoteService>();

	[Test]
	[MethodDataSource(nameof(Remotes))]
	public async Task Access_ThatNamesASlot_NamesOneWhoseRemoteIsTheSameService(RemoteService remote)
	{
		Optional<RemoteService> named = remote.Access switch
		{
			UserKey key => Some.Of(key.Slot.Remote),
			AppInstance app => Some.Of(app.App.Remote),
			UserSignIn or Open => new None(),
		};

		await Assert.That(named.Map(back => back == remote).GetValueOr(true)).IsTrue();
	}

	[Test]
	public async Task EveryKeySlot_ButTmdbsReadToken_IsNamedByTheAccessOfItsOwnRemote()
	{
		await Assert
			.That(
				Enum.GetValues<ApiKeyProvider>()
					.Where(slot => slot is not ApiKeyProvider.TmdbReadToken && (slot.Remote.Access is not UserKey(var named) || named != slot))
			)
			.IsEmpty();
	}

	[Test]
	public async Task TmdbReadToken_IsAnOptionalTmdbSlotThatNoAccessNames()
	{
		await Assert.That(ApiKeyProvider.TmdbReadToken.Remote).IsEqualTo(RemoteService.Tmdb);
		await Assert.That(Enum.GetValues<RemoteService>().Where(remote => remote.Access is UserKey(ApiKeyProvider.TmdbReadToken))).IsEmpty();
	}

	[Test]
	public async Task EveryArrApp_IsNamedByTheAccessOfItsOwnRemote()
	{
		await Assert.That(Enum.GetValues<ArrApp>().Where(app => app.Remote.Access is not AppInstance(var named) || named != app)).IsEmpty();
	}

	[Test]
	public async Task Wikidata_IsOpenAndOwnsNoIds()
	{
		await Assert.That(RemoteService.Wikidata.Access).IsOfUnionType<Access, Open>();
		await Assert.That(RemoteService.Wikidata.OwnIds).IsNone();
	}
}
