using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Shared.Tests;

public class SecretTests
{
	private const string Value = "canary-secret";

	[Test]
	public async Task ToString_Always_PrintsAMask()
	{
		await Assert.That(new Secret(Value).ToString()).IsEqualTo("***");
	}

	[Test]
	public async Task ToString_OfARecordHoldingOne_HidesTheValue()
	{
		var holder = new Holder(new Secret(Value), Some.Of(new Secret(Value)));

		await Assert.That(holder.ToString()).DoesNotContain(Value);
	}

	[Test]
	public async Task Equals_ForTheSameValue_IsTrue()
	{
		await Assert.That(new Secret(Value)).IsEqualTo(new Secret(Value));
	}

	[Test]
	public async Task Reveal_Always_ReturnsTheValue()
	{
		await Assert.That(new Secret(Value).Reveal()).IsEqualTo(Value);
	}

	[Test]
	public async Task Serialize_WithSystemTextJson_WritesNoValue()
	{
		await Assert.That(JsonSerializer.Serialize(new Holder(new Secret(Value), new None()))).DoesNotContain(Value);
	}

	[Test]
	public async Task Bind_FromConfiguration_ReadsTheValue()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Code"] = Value }).Build();

		var bound = configuration.Get<Bound>()!;

		await Assert.That(bound.Code!.Reveal()).IsEqualTo(Value);
	}

	private sealed record Holder(Secret Key, Optional<Secret> Spare);

	private sealed class Bound
	{
		public Secret? Code { get; set; }
	}
}
