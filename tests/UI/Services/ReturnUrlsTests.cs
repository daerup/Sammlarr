using UI.Shared.Services;

namespace UI.Tests.Services;

public class ReturnUrlsTests
{
	[Test]
	[Arguments("/feeds?tab=runs", "/feeds?tab=runs")]
	[Arguments("/", "/")]
	[Arguments(null, "/")]
	[Arguments("", "/")]
	[Arguments("https://evil.example", "/")]
	[Arguments("//evil.example", "/")]
	[Arguments("/\\evil.example", "/")]
	[Arguments("/\t/evil.example", "/")]
	[Arguments("feeds", "/")]
	public async Task Target_KeepsOnlyPathsOnThisSite(string? returnUrl, string expected)
	{
		await Assert.That(ReturnUrls.Target(returnUrl)).IsEqualTo(expected);
	}
}
