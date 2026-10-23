using System.Net;
using API.Mdblist;
using Domain.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistKeyCheckTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task CheckAsync_ForAKeyMdblistAccepts_IsValidAndSendsTheKeyAsQueryParameter()
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(HttpStatusCode.OK);
		var testee = new MdblistKeyCheck(
			new SingleClientHttpClientFactory(handler.CreateClient(BaseUrl)),
			new MdblistOptions { BaseUrl = BaseUrl },
			new MdblistKeyFlights()
		);

		await Assert.That(await testee.CheckAsync(new Secret("mdblist-key"))).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.PathMatches(".*apikey=mdblist-key.*"), Times.Once);
	}

	[Test]
	public async Task CheckAsync_WhenMdblistAnswersALimitInA200_IsSpent()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("""{"error":"API limit reached!"}""");
		using var host = MdblistApis.Module(handler);
		using var scope = host.Services.CreateScope();

		var check = await scope.ServiceProvider.GetRequiredService<MdblistKeyCheck>().CheckAsync(new Secret("mdblist-key"));

		await Assert.That(check).IsEqualTo(AccessCheck.Spent);
	}

	[Test]
	public async Task CheckAsync_ForAnAnswer_RecordsTheRequestsTheCheckedKeyHasLeft()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{}").WithHeader("x-ratelimit-remaining", "961").WithHeader("x-ratelimit-reset", MdblistApis.Reset);
		using var host = MdblistApis.Module(handler);
		using var scope = host.Services.CreateScope();

		await scope.ServiceProvider.GetRequiredService<MdblistKeyCheck>().CheckAsync(new Secret("mdblist-key"));

		await Assert
			.That(host.Services.GetRequiredService<MdblistBudget>().Today("mdblist-key").Answered.Map(answered => answered.Remaining))
			.IsSome()
			.And.IsEqualTo(961);
	}

	[Test]
	public async Task CheckAsync_ForAKeyAtTheFeedReserve_StillAsks()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{}").WithHeader("x-ratelimit-remaining", "3").WithHeader("x-ratelimit-reset", MdblistApis.Reset);
		using var host = MdblistApis.Module(handler);
		using var scope = host.Services.CreateScope();
		var testee = scope.ServiceProvider.GetRequiredService<MdblistKeyCheck>();
		await testee.CheckAsync(new Secret("mdblist-key"));

		var check = await testee.CheckAsync(new Secret("mdblist-key"));

		await Assert.That(check).IsEqualTo(AccessCheck.Valid);
		handler.Verify(request => request.PathMatches(".*apikey=mdblist-key.*"), Times.Exactly(2));
	}
}
