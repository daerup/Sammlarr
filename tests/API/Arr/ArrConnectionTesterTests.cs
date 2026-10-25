using System.Net;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
using Contracts.Settings;
using Domain.Identity;
using Domain.Wantlist.Arr;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Arr;

public class ArrConnectionTesterTests
{
	private static readonly ArrInstanceConfig Radarr = ArrInstanceConfig.Empty with
	{
		Url = Some.Of("http://radarr:7878/"),
		ApiKey = Some.Of(new Secret("0123456789abcdef0123456789abcdef")),
	};

	private readonly MockHttpHandler _radarr = Mock.HttpHandler();

	private readonly TestCurrentUser _user = new();

	private ArrConnectionTester Testee
	{
		get
		{
			return new(
				new ArrApis(new RadarrApi(new HttpClient(_radarr), new RadarrOptions()), new SonarrApi(new HttpClient(Mock.HttpHandler()), new SonarrOptions())),
				_user
			);
		}
	}

	[Test]
	public async Task TestAsync_WhenRadarrAnswers_ReadsItsQualityProfilesAndRootFolders()
	{
		_radarr.OnGet("/api/v3/system/status").RespondWithJson("""{"appName":"Radarr","version":"6.4.4"}""");
		_radarr.OnGet("/api/v3/qualityprofile").RespondWithJson("""[{"id":1,"name":"Any"},{"id":4,"name":"HD-1080p"}]""");
		_radarr.OnGet("/api/v3/rootfolder").RespondWithJson("""[{"id":1,"path":"/movies","freeSpace":1200000000000}]""");

		var tested = await Testee.TestAsync(ArrApp.Radarr, Radarr);

		await Assert
			.That(tested)
			.IsSuccess()
			.And.Multiple(async choices =>
			{
				await Assert.That(choices.Profiles).IsEquivalentTo([new ArrQualityProfile(1, "Any"), new ArrQualityProfile(4, "HD-1080p")]);
				await Assert.That(choices.Folders).IsEquivalentTo([new ArrRootFolder("/movies", 1200000000000)]);
			});
	}

	[Test]
	public async Task TestAsync_WhenTheAddressAnswersAsSonarr_SaysToCheckTheUrl()
	{
		_radarr.OnGet("/api/v3/system/status").RespondWithJson("""{"appName":"Sonarr","version":"4.0.20"}""");

		var tested = await Testee.TestAsync(ArrApp.Radarr, Radarr);

		await Assert.That(tested).IsError();
		await Assert.That(tested.Match(_ => string.Empty, error => error.Reason)).IsEqualTo("http://radarr:7878 answers as Sonarr, not Radarr. Check the URL.");
		await Assert.That(tested).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<ArrKeyRefusedException>()).IsNone());
		_radarr.Verify(request => request.Path("/api/v3/qualityprofile"), Times.Never);
	}

	[Test]
	public async Task TestAsync_WhenRadarrRefusesTheKey_SaysWhereToFixIt()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);

		var tested = await Testee.TestAsync(ArrApp.Radarr, Radarr);

		await Assert
			.That(tested.Match(_ => string.Empty, error => error.Reason))
			.IsEqualTo("Radarr answered 401 Unauthorized at http://radarr:7878. Check the API key under Settings › General in Radarr.");
		await Assert.That(tested).IsError().And.Multiple(async error => await Assert.That(error.CausedBy<ArrKeyRefusedException>()).IsSome());
	}

	[Test]
	public async Task TestAsync_WithoutAnApiKey_SaysWhatIsMissingWithoutCallingRadarr()
	{
		var tested = await Testee.TestAsync(ArrApp.Radarr, Radarr with { ApiKey = new None() });

		await Assert.That(tested.Match(_ => string.Empty, error => error.Reason)).IsEqualTo("Radarr is not set up: it needs an address and an API key.");
		await Assert.That(_radarr.Requests).IsEmpty();
	}

	[Test]
	[Arguments("192.168.1.5:7878", "Radarr's address is not a valid URL: it has to start with http:// or https://.")]
	[Arguments("radarr:7878", "Radarr's address is not a valid URL: it has to start with http:// or https://.")]
	[Arguments("ftp://radarr:7878", "Radarr's address is not a valid URL: it has to start with http:// or https://.")]
	[Arguments("http://radarr:78789", "Radarr's address is not a valid URL.")]
	public async Task TestAsync_ForAnInvalidAddress_SaysWhatIsWrongWithoutCallingRadarr(string url, string reason)
	{
		var tested = await Testee.TestAsync(ArrApp.Radarr, Radarr with { Url = Some.Of(url) });

		await Assert.That(tested.Match(_ => string.Empty, error => error.Reason)).IsEqualTo(reason);
		await Assert.That(_radarr.Requests).IsEmpty();
	}

	[Test]
	public async Task TestAsync_AsAMember_Refuses()
	{
		_user.Role = UserRole.Member;

		await Assert.That(async () => await Testee.TestAsync(ArrApp.Radarr, Radarr)).Throws<UnauthorizedAccessException>();
	}
}
