using API.Mdblist;
using Contracts;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Mdblist;

internal static class MdblistApis
{
	internal static readonly MdblistOptions Options = new() { BaseUrl = "https://api.mdblist.com" };

	internal static MdblistTitles Titles(MockHttpHandler handler, MdblistBudget? budget = null)
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Mdblist, "mdblist-key")));
		var client = new MdblistClient(handler.CreateClient(Options.BaseUrl), Options, keys.Object, ICurrentUser.Mock().Object);
		return new MdblistTitles(client, keys.Object, budget ?? new MdblistBudget(Clock(), Options));
	}

	internal static IDateTimeProvider Clock()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(DateTimeOffset.UnixEpoch.AddDays(1));
		return clock.Object;
	}

	internal static Task<string> RecordedAsync(string name) => File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Mdblist", "Recorded", name));
}
