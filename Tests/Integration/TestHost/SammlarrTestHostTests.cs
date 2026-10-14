using Harness;

namespace Integration.Tests.TestHost;

public class SammlarrTestHostTests
{
	[Test]
	public async Task DisposeAsync_AfterAnUnstubbedCall_FailsNamingTheUrl()
	{
		var host = new SammlarrTestHost();
		using var client = new HttpClient(host.HttpStub, disposeHandler: false);
		await client.GetAsync(new Uri("https://nowhere.test/stray"));

		await Assert.That(async () => await host.DisposeAsync()).Throws<InvalidOperationException>().WithMessageContaining("https://nowhere.test/stray");
	}
}
