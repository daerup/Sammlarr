using Microsoft.AspNetCore.Http;
using Web.Instrumentation;

namespace Web.Tests.Instrumentation;

public class InstrumentationModuleTests
{
	[Test]
	[Arguments("/alive")]
	[Arguments("/health")]
	[Arguments("/_blazor/negotiate")]
	[Arguments("/css/app.css")]
	public async Task IsMeaningfulRequest_ForAProbeOrAnAsset_IsNotTraced(string path)
	{
		await Assert.That(InstrumentationModule.IsMeaningfulRequest(Request(path))).IsFalse();
	}

	[Test]
	[Arguments("/feeds")]
	[Arguments("/auth/plex/callback")]
	public async Task IsMeaningfulRequest_ForAPage_IsTraced(string path)
	{
		await Assert.That(InstrumentationModule.IsMeaningfulRequest(Request(path))).IsTrue();
	}

	private static DefaultHttpContext Request(string path) => new() { Request = { Path = path } };
}
