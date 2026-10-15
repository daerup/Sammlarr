using Microsoft.AspNetCore.Http;
using Shared.Options;
using Web.Auth;

namespace Web.Tests.Auth;

public class PublicOriginTests
{
	[Test]
	public async Task Of_WithAPublicUrl_UsesItWithoutItsTrailingSlash()
	{
		var options = new SammlarOptions { ConnectionString = string.Empty, PublicUrl = " https://sammlarr.example.com/ " };

		await Assert.That(PublicOrigin.Of(options, Proxied())).IsEqualTo("https://sammlarr.example.com");
	}

	[Test]
	[Arguments("")]
	[Arguments("  ")]
	public async Task Of_WithoutAPublicUrl_UsesTheRequest(string publicUrl)
	{
		var options = new SammlarOptions { ConnectionString = string.Empty, PublicUrl = publicUrl };

		await Assert.That(PublicOrigin.Of(options, Proxied())).IsEqualTo("http://sammlarr:6769");
	}

	private static HttpRequest Proxied()
	{
		var request = new DefaultHttpContext().Request;
		request.Scheme = "http";
		request.Host = new HostString("sammlarr", 6769);
		return request;
	}
}
