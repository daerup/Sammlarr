using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Feed;

namespace DB.Conversion.Feed;

internal sealed class FeedRunTriggerJsonConverter : JsonConverter<FeedRunTrigger>
{
	public override FeedRunTrigger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		FeedRunTrigger.Parse(reader.GetString()!);

	public override void Write(Utf8JsonWriter writer, FeedRunTrigger value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
