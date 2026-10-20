using System.Net;
using API.Http;
using API.Http.Lanes;
using API.Mdblist;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Mdblist;

internal static class MdblistApis
{
	internal static readonly MdblistOptions Options = new()
	{
		BaseUrl = "https://api.mdblist.com",
		FeedReserve = 120,
		Http = new ProviderHttpOptions { Lanes = new Dictionary<string, LaneOptions> { ["titles"] = new LaneOptions { MaxBatch = 200 } } },
	};

	internal static MdblistTitles Titles(MockHttpHandler handler, TimeProvider? clock = null)
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Mdblist, "mdblist-key")));
		var client = new MdblistClient(handler.CreateClient(Options.BaseUrl), Options, keys.Object, ICurrentUser.Mock().Object);
		return new MdblistTitles(client, keys.Object, Lanes(), clock ?? Clock());
	}

	private static Lanes Lanes()
	{
		return new(TimeProvider.System, [new RemoteSettings(RemoteService.Mdblist, Options)], new ServiceCollection().BuildServiceProvider());
	}

	internal static string Reset => $"{DateTimeOffset.UnixEpoch.AddDays(2).ToUnixTimeSeconds()}";

	internal static MdblistBudget Budget() => new(Clock(), Options, NullLogger<MdblistBudget>.Instance);

	internal static IHost Module(MockHttpHandler handler)
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddLogging();
				services.AddSingleton(
					new MdblistOptions
					{
						BaseUrl = Options.BaseUrl,
						Http = new ProviderHttpOptions { AttemptTimeoutSeconds = 1 },
					}
				);
				services.AddModule<HttpModule>();
				services.AddSingleton(Clock());
				services.AddSingleton(IProviderRateLimitReporter.Mock().Object);
				services.AddSingleton(IFeedRunUsage.Mock().Object);
				services.AddSingleton(IApiKeyService.Mock().Object);
				services.AddSingleton(ICurrentUser.Mock().Object);
				new MdblistModule().RegisterServices(services);
				services.AddHttpClient<MdblistClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
				services.AddHttpClient(MdblistClient.Unauthenticated).ConfigurePrimaryHttpMessageHandler(() => handler);
			})
			.Build();
	}

	internal static TimeProvider Clock() => new FrozenClock(DateTimeOffset.UnixEpoch.AddDays(1));

	internal static Task<string> RecordedAsync(string name) => File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Mdblist", "Recorded", name));

	internal static HttpResponseMessage RateLimited(long remaining, DateTimeOffset reset, long? limit = null, HttpStatusCode status = HttpStatusCode.OK)
	{
		var response = new HttpResponseMessage(status);
		response.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		response.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		Optional.Of(limit).Apply(value => response.Headers.Add("x-ratelimit-limit", $"{value}"));
		return response;
	}

	private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
