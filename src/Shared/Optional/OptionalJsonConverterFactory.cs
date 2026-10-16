using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		return (JsonConverter)Activator.CreateInstance(typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
	}

	private sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
		where T : notnull
	{
		public override bool HandleNull => true;

		public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return reader.TokenType == JsonTokenType.Null ? new None() : Some.Of(JsonSerializer.Deserialize<T>(ref reader, options)!);
		}

		public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
		{
			value.Apply(present => JsonSerializer.Serialize(writer, present, options)).ApplyNone(writer.WriteNullValue);
		}
	}
}
