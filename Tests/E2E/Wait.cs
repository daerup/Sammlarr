using Microsoft.Playwright;

namespace E2E.Tests;

internal static class Wait
{
	internal const float Settle = 30_000;

	internal const float Preview = 60_000;

	extension(ILocatorAssertions assertions)
	{
		internal Task ToBeVisibleAsync(float timeout) => assertions.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeout });

		internal Task ToBeHiddenAsync(float timeout) => assertions.ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = timeout });

		internal Task ToBeEnabledAsync(float timeout) => assertions.ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = timeout });

		internal Task ToBeDisabledAsync(float timeout) => assertions.ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = timeout });

		internal Task ToHaveCountAsync(int count, float timeout) =>
			assertions.ToHaveCountAsync(count, new LocatorAssertionsToHaveCountOptions { Timeout = timeout });

		internal Task ToHaveTextAsync(string text, float timeout) => assertions.ToHaveTextAsync(text, new LocatorAssertionsToHaveTextOptions { Timeout = timeout });
	}
}
