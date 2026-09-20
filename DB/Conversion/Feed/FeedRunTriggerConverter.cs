using Domain.Feed;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Feed;

internal sealed class FeedRunTriggerConverter : ValueConverter<FeedRunTrigger, string>
{
	private const string ManualLiteral = "manual";

	public FeedRunTriggerConverter()
		: base(mode => ToText(mode), text => FromText(text)) { }

	private static string ToText(FeedRunTrigger mode) =>
		mode switch
		{
			Scheduled scheduled => scheduled.ToString(),
			Manual => ManualLiteral,
		};

	private static FeedRunTrigger FromText(string text) => text == ManualLiteral ? new FeedRunTrigger(new Manual()) : new FeedRunTrigger(new Scheduled(text));
}
