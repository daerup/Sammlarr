using Domain.Feed;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Feed;

internal sealed class FeedRunTriggerConverter : ValueConverter<FeedRunTrigger, string>
{
	public FeedRunTriggerConverter()
		: base(trigger => trigger.ToString(), text => FeedRunTrigger.Parse(text)) { }
}
