using System.Globalization;
using Domain.Feed;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Feed;

internal sealed class FeedStrategyConverter : ValueConverter<FeedStrategy, string>
{
	private const string AppendLiteral = "append";
	private const string MirrorLiteral = "mirror";

	public FeedStrategyConverter()
		: base(strategy => ToText(strategy), text => FromText(text)) { }

	private static string ToText(FeedStrategy strategy) =>
		strategy switch
		{
			Mirror mirror => FormattableString.Invariant($"{MirrorLiteral}:{mirror.AbsenceThreshold}"),
			Append => AppendLiteral,
		};

	private static FeedStrategy FromText(string text)
	{
		var parts = text.Split(':');
		return parts[0] switch
		{
			MirrorLiteral => new FeedStrategy(new Mirror(int.Parse(parts[1], CultureInfo.InvariantCulture))),
			_ => new FeedStrategy(new Append()),
		};
	}
}
