using Domain.Media;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Ratings;

internal sealed class RatingStrategyConverter : ValueConverter<RatingStrategy, string>
{
	private const string AveragedLiteral = "averaged";
	private const string OrderedLiteral = "ordered";

	public RatingStrategyConverter()
		: base(strategy => ToText(strategy), text => FromText(text)) { }

	private static string ToText(RatingStrategy strategy)
	{
		return strategy switch
		{
			OrderedRating ordered => Listed(OrderedLiteral, ordered.Order),
			AveragedRating averaged => Listed(AveragedLiteral, averaged.Sources.Order()),
		};
	}

	private static string Listed(string literal, IEnumerable<RatingSource> sources)
	{
		var named = string.Join(',', sources);
		return named.Length == 0 ? literal : $"{literal}:{named}";
	}

	private static RatingStrategy FromText(string text)
	{
		var parts = text.Split(':');
		var sources = parts.Skip(1).SelectMany(list => list.Split(',')).Select(Enum.Parse<RatingSource>).ToList();
		return parts[0] switch
		{
			OrderedLiteral => new RatingStrategy(new OrderedRating(sources)),
			_ => new RatingStrategy(new AveragedRating([.. sources])),
		};
	}
}
