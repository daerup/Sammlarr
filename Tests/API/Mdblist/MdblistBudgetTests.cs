using System.Net;
using API.Mdblist;
using Microsoft.Extensions.Logging;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistBudgetTests
{
	[Test]
	public async Task LeavesReserve_OnceTheReserveIsReached_WarnsOncePerReset()
	{
		var logger = new CountingLogger();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 120));

		var first = budget.LeavesReserve("mdblist-key");
		var second = budget.LeavesReserve("mdblist-key");

		await Assert.That(first || second).IsFalse();
		await Assert.That(logger.Warnings).IsEqualTo(1);
	}

	[Test]
	public async Task LeavesReserve_WhileRequestsAreLeftAboveTheReserve_WarnsNothing()
	{
		var logger = new CountingLogger();
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, logger);
		budget.Record("mdblist-key", Headers(remaining: 121));

		await Assert.That(budget.LeavesReserve("mdblist-key")).IsTrue();
		await Assert.That(logger.Warnings).IsEqualTo(0);
	}

	[Test]
	public async Task Left_BeforeTheReset_IsWhatMdblistLastSaid()
	{
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, new CountingLogger());
		budget.Record("mdblist-key", Headers(remaining: 962));

		await Assert.That(budget.Left("mdblist-key")).IsSome().And.IsEqualTo(962);
		await Assert.That(budget.Left("another-key")).IsNone();
	}

	[Test]
	public async Task Left_AfterTheReset_IsUnknown()
	{
		var budget = new MdblistBudget(MdblistApis.Clock(), MdblistApis.Options, new CountingLogger());
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

	private sealed class CountingLogger : ILogger<MdblistBudget>
	{
		public int Warnings { get; private set; }

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (logLevel is LogLevel.Warning)
			{
				Warnings++;
			}
		}
	}
}
