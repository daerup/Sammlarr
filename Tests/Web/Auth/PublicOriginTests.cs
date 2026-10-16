using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.DI;
using Shared.Options;
using Web.Auth;

namespace Web.Tests.Auth;

public class PublicOriginTests
{
	[Test]
	public async Task Of_WithAPublicUrl_UsesItWithoutItsTrailingSlash()
	{
		await Assert.That(PublicOrigin.Of(Options(" https://sammlarr.example.com/ "), Proxied())).IsEqualTo("https://sammlarr.example.com");
	}

	[Test]
	[Arguments("")]
	[Arguments("  ")]
	public async Task Of_WithoutAPublicUrl_UsesTheRequest(string publicUrl)
	{
		await Assert.That(PublicOrigin.Of(Options(publicUrl), Proxied())).IsEqualTo("http://sammlarr:6769");
	}

	[Test]
	[Arguments("")]
	[Arguments("https://sammlarr.example.com")]
	[Arguments(" https://sammlarr.example.com/ ")]
	[Arguments("http://192.168.1.5:6769")]
	public async Task IsValid_ForNothingOrARootAddress_IsTrue(string publicUrl)
	{
		await Assert.That(PublicOrigin.IsValid(Options(publicUrl))).IsTrue();
	}

	[Test]
	[Arguments("sammlarr.example.com")]
	[Arguments("https://example.com/sammlarr")]
	[Arguments("https://sammlarr.example.com/?from=proxy")]
	[Arguments("https://sammlarr.example.com/#signin")]
	[Arguments("https://ada:secret@sammlarr.example.com")]
	[Arguments("ftp://sammlarr.example.com")]
	public async Task IsValid_ForAnAddressPlexCannotReturnTo_IsFalse(string publicUrl)
	{
		await Assert.That(PublicOrigin.IsValid(Options(publicUrl))).IsFalse();
	}

	[Test]
	public async Task Start_WithAPublicUrlWithoutItsScheme_FailsNamingTheSetting()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.Services.Configure<SammlarOptions>(options => options.PublicUrl = "sammlarr.example.com");
		builder.Services.AddModule<AuthenticationModule>();
		await using var app = builder.Build();

		await Assert.That(() => app.StartAsync()).Throws<OptionsValidationException>().WithMessageContaining("Sammlarr__PublicUrl");
	}

	private static SammlarOptions Options(string publicUrl) => new() { ConnectionString = string.Empty, PublicUrl = publicUrl };

	private static HttpRequest Proxied()
	{
		var request = new DefaultHttpContext().Request;
		request.Scheme = "http";
		request.Host = new HostString("sammlarr", 6769);
		return request;
	}
}
