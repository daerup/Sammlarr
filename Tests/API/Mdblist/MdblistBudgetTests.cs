using System.Net;
using API.Http.Lanes;
using API.Mdblist;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistBudgetTests
{
	[Test]
	public async Task ReservedUntil_OnceTheReserveIsReached_IsTheResetAndWarnsOncePerReset()
	{
		var logger = new CountingLogger<MdblistBudget>();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 120));

		var first = budget.ReservedUntil("mdblist-key");
		var second = budget.ReservedUntil("mdblist-key");

		await Assert.That(first).IsSome().And.IsEqualTo(DateTimeOffset.UnixEpoch.AddDays(2));
		await Assert.That(second).IsSome();
		await Assert.That(logger.Warnings).IsEqualTo(1);
	}

	[Test]
	public async Task ReservedUntil_WhileRequestsAreLeftAboveTheReserve_IsNoneAndWarnsNothing()
	{
		var logger = new CountingLogger<MdblistBudget>();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 121));

		await Assert.That(budget.ReservedUntil("mdblist-key")).IsNone();
		await Assert.That(logger.Warnings).IsEqualTo(0);
	}

	[Test]
	public async Task Record_ForARequestStampedWithTheLaneOwner_CountsItForThatKey()
	{
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, new CountingLogger<MdblistBudget>());
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", "640");
		answered.Headers.Add("x-ratelimit-reset", $"{DateTimeOffset.UnixEpoch.AddDays(2).ToUnixTimeSeconds()}");
		var wire = Mock.HttpHandler();
		wire.OnAnyRequest().RespondWith(answered);
		using var invoker = new HttpMessageInvoker(new MdblistBudgetHandler(budget) { InnerHandler = wire });
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.mdblist.com/lists/1/items");
		request.Options.Set(LaneHandler.Owner, "mdblist-key");

		using var response = await invoker.SendAsync(request, CancellationToken.None);

		await Assert.That(budget.Left("mdblist-key")).IsSome().And.IsEqualTo(640);
	}

	[Test]
	public async Task Left_BeforeTheReset_IsWhatMdblistLastSaid()
	{
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, new CountingLogger<MdblistBudget>());
		budget.Record("mdblist-key", Headers(remaining: 962));

		await Assert.That(budget.Left("mdblist-key")).IsSome().And.IsEqualTo(962);
		await Assert.That(budget.Left("another-key")).IsNone();
	}

	[Test]
	public async Task Left_AfterTheReset_IsUnknown()
	{
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, new CountingLogger<MdblistBudget>());
		var response = new HttpResponseMessage(HttpStatusCode.OK);
		response.Headers.Add("x-ratelimit-remaining", "12");
		response.Headers.Add("x-ratelimit-reset", $"{DateTimeOffset.UnixEpoch.AddHours(12).ToUnixTimeSeconds()}");
		budget.Record("mdblist-key", response.Headers);

		await Assert.That(budget.Left("mdblist-key")).IsNone();
	}

	private static System.Net.Http.Headers.HttpResponseHeaders Headers(long remaining)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK);
		response.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		response.Headers.Add("x-ratelimit-reset", $"{DateTimeOffset.UnixEpoch.AddDays(2).ToUnixTimeSeconds()}");
		return response.Headers;
	}
}
