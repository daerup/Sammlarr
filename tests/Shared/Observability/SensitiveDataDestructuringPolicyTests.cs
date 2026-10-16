using Contracts;
using Domain.Notifications;
using Domain.Wantlist.Arr;
using Observability.Logging;
using Serilog;
using Serilog.Events;
using TestInfrastructure;

namespace Shared.Tests.Observability;

public class SensitiveDataDestructuringPolicyTests
{
	private sealed record Account(string Username, [property: Sensitive] string Token);

	private sealed record Plain(string Name);

	private static SensitiveDataDestructuringPolicy WithModel() => new(SensitiveModel.Build());

	private static SensitiveDataDestructuringPolicy WithoutModel() => new(new SensitiveModelBuilder().Build());

	[Test]
	public async Task Destructure_MasksAttributeSensitiveProperty_AndKeepsOthers()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(WithoutModel()).WriteTo.Sink(captured).CreateLogger();

		logger.Information("Loaded {@Account}", new Account("john", "super-secret-token"));

		var structure = (StructureValue)captured.Last.Properties["Account"];
		var username = ((ScalarValue)structure.Properties.Single(p => p.Name == "Username").Value).Value;
		var token = structure.Properties.Single(p => p.Name == "Token").Value.ToString();

		await Assert.That(username).IsEqualTo("john");
		await Assert.That(token).DoesNotContain("super-secret-token");
		await Assert.That(captured.Last.RenderMessage()).DoesNotContain("super-secret-token");
	}

	[Test]
	public async Task Destructure_MasksRegistrySensitiveProperty_AndKeepsOthers()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(WithModel()).WriteTo.Sink(captured).CreateLogger();

		logger.Information("Loaded {@Config}", new WebhookConfig("https://example.test/hook", "whsec_super-secret"));

		var structure = (StructureValue)captured.Last.Properties["Config"];
		var url = ((ScalarValue)structure.Properties.Single(p => p.Name == "Url").Value).Value;
		var secret = structure.Properties.Single(p => p.Name == "SigningSecret").Value.ToString();

		await Assert.That(url).IsEqualTo("https://example.test/hook");
		await Assert.That(secret).DoesNotContain("whsec_super-secret");
		await Assert.That(captured.Last.RenderMessage()).DoesNotContain("whsec_super-secret");
	}

	[Test]
	public async Task Destructure_MasksBothArrApiKeysInsideTheSinkConfig()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(WithModel()).WriteTo.Sink(captured).CreateLogger();
		var radarr = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of("radarr-super-secret") };
		var sonarr = ArrInstanceConfig.Empty with { Url = Some.Of("http://sonarr:8989"), ApiKey = Some.Of("sonarr-super-secret") };

		logger.Information("Loaded {@Config}", new ArrSinkConfig(radarr, sonarr, ArrTagScheme.Default));

		var rendered = captured.Last.RenderMessage();
		await Assert.That(rendered).DoesNotContain("radarr-super-secret");
		await Assert.That(rendered).DoesNotContain("sonarr-super-secret");
		await Assert.That(rendered).Contains("Monitored");
	}

	[Test]
	public async Task Destructure_LeavesTypesWithoutSensitiveProperties_Intact()
	{
		var captured = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(WithoutModel()).WriteTo.Sink(captured).CreateLogger();

		logger.Information("Loaded {@Plain}", new Plain("hello"));

		var structure = (StructureValue)captured.Last.Properties["Plain"];
		var name = ((ScalarValue)structure.Properties.Single(p => p.Name == "Name").Value).Value;

		await Assert.That(name).IsEqualTo("hello");
	}
}
