using System.Globalization;

namespace E2E.Tests;

internal static class TestData
{
	internal static string Name(string scenario) => $"E2E {scenario} {Guid.NewGuid().ToString("N")[..6]}";

	internal static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
