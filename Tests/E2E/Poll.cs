namespace E2E.Tests;

internal static class Poll
{
	private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

	internal static async Task UntilAsync(Func<Task<bool>> done, Func<string> failure)
	{
		var deadline = DateTimeOffset.UtcNow + Timeout;
		while (!await done())
		{
			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new TimeoutException($"{failure()} within {Timeout}");
			}

			await Task.Delay(500);
		}
	}
}
