using System.Text.Json;
using TestInfrastructure;

namespace Shared.Tests;

public class OptionalJsonConverterFactoryTests
{
	private static readonly JsonSerializerOptions Options = new() { Converters = { new OptionalJsonConverterFactory() } };

	[Test]
	public async Task Serialize_ASome_WritesItsValue()
	{
		Optional<string> optional = Some.Of("hello");

		await Assert.That(JsonSerializer.Serialize(optional, Options)).IsEqualTo("\"hello\"");
	}

	[Test]
	public async Task Serialize_ANone_WritesNull()
	{
		Optional<int> optional = new None();

		await Assert.That(JsonSerializer.Serialize(optional, Options)).IsEqualTo("null");
	}

	[Test]
	public async Task Deserialize_AValue_ReadsItAsSome()
	{
		await Assert.That(JsonSerializer.Deserialize<Optional<int>>("42", Options)).IsSome().And.IsEqualTo(42);
	}

	[Test]
	public async Task Deserialize_Null_ReadsItAsNone()
	{
		await Assert.That(JsonSerializer.Deserialize<Optional<string>>("null", Options)).IsNone();
	}
}
