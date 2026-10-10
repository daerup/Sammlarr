using Domain.Wantlist.Arr;
using TestInfrastructure;

namespace Domain.Tests.Wantlist.Arr;

public class ArrSinkConfigTests
{
	private static readonly ArrInstanceConfig Connectable = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of("key") };

	private static readonly ArrInstanceConfig SetUp = Connectable with { QualityProfileId = Some.Of(4), RootFolderPath = Some.Of("/movies") };

	public static IEnumerable<Func<ArrInstanceConfig>> MissingOneSetting() =>
		[
			() => SetUp with { Url = new None() },
			() => SetUp with { ApiKey = new None() },
			() => SetUp with { QualityProfileId = new None() },
			() => SetUp with { RootFolderPath = new None() },
		];

	public static IEnumerable<Func<ArrInstanceConfig>> MissingAnAddressOrAKey() =>
		[() => Connectable with { Url = new None() }, () => Connectable with { ApiKey = new None() }];

	[Test]
	[Arguments(ArrApp.Radarr)]
	[Arguments(ArrApp.Sonarr)]
	public async Task With_AnApp_ChangesOnlyThatAppsInstance(ArrApp app)
	{
		var changed = ArrSinkConfig.Empty.With(app, SetUp);

		await Assert.That(changed.Of(app)).IsEqualTo(SetUp);
		await Assert.That(changed.Of(Other(app))).IsEqualTo(ArrInstanceConfig.Empty);
		await Assert.That(changed.Tags).IsEqualTo(ArrTagScheme.Default);
	}

	[Test]
	public async Task LinkUrl_WithoutAnExternalUrl_IsTheUrl()
	{
		await Assert.That(Connectable.LinkUrl).IsSome().And.IsEqualTo("http://radarr:7878");
	}

	[Test]
	public async Task LinkUrl_WithAnExternalUrl_IsTheExternalUrl()
	{
		var linked = Connectable with { ExternalUrl = Some.Of("http://localhost:7879") };

		await Assert.That(linked.LinkUrl).IsSome().And.IsEqualTo("http://localhost:7879");
	}

	[Test]
	[MethodDataSource(nameof(MissingOneSetting))]
	public async Task IsSetUp_ForAnInstanceMissingOneSetting_IsFalse(ArrInstanceConfig instance)
	{
		await Assert.That(instance.IsSetUp).IsFalse();
	}

	[Test]
	public async Task IsSetUp_NeedsBothApps()
	{
		await Assert.That(new ArrSinkConfig(SetUp, SetUp, ArrTagScheme.Default).IsSetUp).IsTrue();
		await Assert.That(new ArrSinkConfig(SetUp, Connectable, ArrTagScheme.Default).IsSetUp).IsFalse();
		await Assert.That(new ArrSinkConfig(Connectable, SetUp, ArrTagScheme.Default).IsSetUp).IsFalse();
	}

	[Test]
	public async Task RoutesAnime_OnceAnAnimeFolderIsChosen()
	{
		await Assert.That(SetUp.RoutesAnime).IsFalse();
		await Assert.That((SetUp with { AnimeQualityProfileId = Some.Of(7) }).RoutesAnime).IsFalse();
		await Assert.That((SetUp with { AnimeRootFolderPath = Some.Of("/anime-movies") }).RoutesAnime).IsTrue();
	}

	[Test]
	public async Task FolderFor_Anime_WithoutAnAnimeFolder_IsTheDefaultFolder()
	{
		await Assert.That(SetUp.FolderFor(ArrRoute.Anime)).IsSome().And.IsEqualTo("/movies");
	}

	[Test]
	public async Task FolderFor_Anime_WithAnAnimeFolder_IsTheAnimeFolder()
	{
		var anime = SetUp with { AnimeRootFolderPath = Some.Of("/anime-movies") };

		await Assert.That(anime.FolderFor(ArrRoute.Anime)).IsSome().And.IsEqualTo("/anime-movies");
		await Assert.That(anime.FolderFor(ArrRoute.Default)).IsSome().And.IsEqualTo("/movies");
	}

	[Test]
	public async Task ProfileFor_Anime_WithoutAnAnimeProfile_IsTheDefaultProfile()
	{
		await Assert.That(SetUp.ProfileFor(ArrRoute.Anime)).IsSome().And.IsEqualTo(4);
	}

	[Test]
	public async Task ProfileFor_Anime_WithAnAnimeProfile_IsTheAnimeProfile()
	{
		var anime = SetUp with { AnimeQualityProfileId = Some.Of(7) };

		await Assert.That(anime.ProfileFor(ArrRoute.Anime)).IsSome().And.IsEqualTo(7);
		await Assert.That(anime.ProfileFor(ArrRoute.Default)).IsSome().And.IsEqualTo(4);
	}

	[Test]
	[MethodDataSource(nameof(MissingAnAddressOrAKey))]
	public async Task IsConnectable_ForAnInstanceMissingItsAddressOrKey_IsFalse(ArrInstanceConfig instance)
	{
		await Assert.That(instance.IsConnectable).IsFalse();
	}

	[Test]
	public async Task IsConnectable_NeedsBothApps()
	{
		await Assert.That(new ArrSinkConfig(Connectable, Connectable, ArrTagScheme.Default).IsConnectable).IsTrue();
		await Assert.That(new ArrSinkConfig(Connectable, ArrInstanceConfig.Empty, ArrTagScheme.Default).IsConnectable).IsFalse();
		await Assert.That(new ArrSinkConfig(ArrInstanceConfig.Empty, Connectable, ArrTagScheme.Default).IsConnectable).IsFalse();
	}

	private static ArrApp Other(ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => ArrApp.Sonarr,
			ArrApp.Sonarr => ArrApp.Radarr,
		};
	}
}
