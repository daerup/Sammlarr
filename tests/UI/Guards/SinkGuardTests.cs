using Bunit;
using Contracts.Auth;
using Contracts.Settings;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class SinkGuardTests : BunitContext
{
	private static readonly ArrInstanceConfig SetUp = new()
	{
		Url = Some.Of("http://radarr:7878"),
		ApiKey = Some.Of(new Secret("key")),
		QualityProfileId = Some.Of(1),
		RootFolderPath = Some.Of("/movies"),
	};

	private readonly Mock<ICredentialService> _credentials = ICredentialService.Mock();

	private readonly Mock<IArrSinkConfigService> _arr = IArrSinkConfigService.Mock();

	[Before(Test)]
	public void Setup()
	{
		_credentials.GetAsync(Any(), Any()).Returns(Error.From("No Plex account connected."));
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		Services.AddSingleton(_credentials.Object);
		Services.AddSingleton(_arr.Object);
	}

	[Test]
	public async Task Ready_ForAPlexSinkWithoutAPlexAccount_IsFalseWhateverTheSignIn()
	{
		await Assert.That(Ready(SinkKind.Plex)).IsFalse();
	}

	[Test]
	public async Task Ready_ForRadarrAndSonarrWithOnlyRadarrSetUp_IsFalse()
	{
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = SetUp });

		await Assert.That(Ready(SinkKind.Arr)).IsFalse();
	}

	[Test]
	public async Task Ready_ForRadarrAndSonarrWithBothSetUp_IsTrueWithoutAnyPlexAccount()
	{
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = SetUp, Sonarr = SetUp with { Url = Some.Of("http://sonarr:8989") } });

		await Assert.That(Ready(SinkKind.Arr)).IsTrue();
	}

	private bool Ready(SinkKind sink)
	{
		bool? reported = null;
		var cut = Render<SinkGuard>(parameters =>
			parameters.Add(guard => guard.Sink, sink).Add(guard => guard.ReadyChanged, EventCallback.Factory.Create<bool>(this, ready => reported = ready))
		);
		cut.WaitForState(() => reported is not null);
		return reported is true;
	}
}
