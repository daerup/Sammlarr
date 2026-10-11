using System.Net;
using API.Http.Lanes;
using API.Mdblist;
using Contracts;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Mdblist;

internal static class MdblistApis
{
	internal static readonly MdblistOptions Options = new()
	{
		BaseUrl = "https://api.mdblist.com",
		Http = new ProviderHttpOptions { Lanes = new() { ["titles"] = new LaneOptions { MaxBatch = 200 } } },
	};

	internal static MdblistTitles Titles(MockHttpHandler handler, IDateTimeProvider? clock = null)
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Mdblist, "mdblist-key")));
		var client = new MdblistClient(handler.CreateClient(Options.BaseUrl), Options, keys.Object, ICurrentUser.Mock().Object);
		return new MdblistTitles(client, keys.Object, Lanes(), clock ?? Clock());
	}

	internal static Lanes Lanes()
	{
		return new(TimeProvider.System, [new RemoteSettings(RemoteService.Mdblist, Options)], new ServiceCollection().BuildServiceProvider());
	}

	internal static MdblistBudget Budget() => new(Clock(), Options, NullLogger<MdblistBudget>.Instance);

	internal static IDateTimeProvider Clock()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(DateTimeOffset.UnixEpoch.AddDays(1));
		return clock.Object;
	}

	internal static Task<string> RecordedAsync(string name) => File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Mdblist", "Recorded", name));

	internal static HttpResponseMessage RateLimited(long remaining, DateTimeOffset reset, long? limit = null, HttpStatusCode status = HttpStatusCode.OK)
	{
		var response = new HttpResponseMessage(status);
		response.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		response.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		Optional.Of(limit).Apply(value => response.Headers.Add("x-ratelimit-limit", $"{value}"));
		return response;
	}
}
