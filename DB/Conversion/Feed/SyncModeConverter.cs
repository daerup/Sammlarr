using Domain.Feed;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Feed;

internal sealed class SyncModeConverter : ValueConverter<SyncMode, string>
{
	private const string ManualLiteral = "manual";

	public SyncModeConverter()
		: base(mode => ToText(mode), text => FromText(text)) { }

	private static string ToText(SyncMode mode) =>
		mode switch
		{
			Scheduled scheduled => scheduled.ToString(),
			Manual => ManualLiteral,
		};

	private static SyncMode FromText(string text) => text == ManualLiteral ? new SyncMode(new Manual()) : new SyncMode(new Scheduled(text));
}
