using System.Globalization;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using Domain.Processing;
using Humanizer;
using UI.Shared.Formatting;
using UI.Shared.Media;
using UI.Shared.Media.Ratings;
using Duration = Domain.Processing.Duration;
using DurationUnit = Domain.Processing.DurationUnit;
using FilterOperator = BlazorBlueprint.Primitives.Filtering.FilterOperator;

namespace UI.Features.Feeds.Pages.Editor.Filters;

internal sealed record WrittenRule(string Field, string Operator, string Value);

internal static class RuleTexts
{
	private const string Unknown = "not known yet";

	private const int LongestText = 60;

	public static WrittenRule Written(FilterCondition condition)
	{
		var field = FeedFields.For(condition.Field);
		var label = field.Kind switch
		{
			FeedFieldKind.Runtime => RuleWords.Runtime(field.IsTotal(condition.Field)),
			FeedFieldKind.Text or FeedFieldKind.Number or FeedFieldKind.Choice or FeedFieldKind.Date or FeedFieldKind.Genres => field.Label,
		};
		var comparison = field
			.Operators.FirstOrNone(offered => offered.Value == condition.Operator)
			.Map(offered => offered.Text)
			.GetValueOr(() => condition.Operator.Humanize(LetterCasing.LowerCase));
		var value =
			condition.Operator is FilterOperator.Between ? $"{Shown(field, condition.Value)} and {Shown(field, condition.ValueEnd)}" : Shown(field, condition.Value);
		return new WrittenRule(label, comparison, value);
	}

	public static string Value(string property, MediaQueryRow row, RatingStrategy strategy)
	{
		return property switch
		{
			nameof(MediaQueryRow.Rating) => row.Sammlarr.Match(rating => $"{RatingTexts.Number(rating)} ({By(rating, strategy)})", () => "no rating yet"),
			nameof(MediaQueryRow.Votes) => row.Sammlarr.Match(rating => rating.Votes.Match(RatingTexts.Votes, () => Unvoted(rating)), () => "no rating yet"),
			nameof(MediaQueryRow.Seasons) => Count(FeedFields.For(property), row.Seasons, row.MediaType),
			nameof(MediaQueryRow.Episodes) => Count(FeedFields.For(property), row.Episodes, row.MediaType),
			nameof(MediaQueryRow.Title) => Text(row.Title),
			nameof(MediaQueryRow.Tagline) => Text(row.Tagline),
			nameof(MediaQueryRow.Summary) => Text(row.Summary),
			nameof(MediaQueryRow.Genres) => row.Genres.Count == 0 ? "none" : string.Join(", ", row.Genres),
			nameof(MediaQueryRow.MediaType) => row.MediaType.Label,
			nameof(MediaQueryRow.ReleaseDate) => Optional.Of(row.ReleaseDate).Map(RuleWords.Day).GetValueOr(Unknown),
			nameof(MediaQueryRow.RuntimeMinutes) => Optional.Of(row.RuntimeMinutes).Map(PerEpisode).GetValueOr(Unknown),
			nameof(MediaQueryRow.TotalRuntimeMinutes) => Optional.Of(row.TotalRuntimeMinutes).Map(InTotal).GetValueOr(Unknown),
			_ => Unknown,
		};
	}

	private static string Shown(FeedField field, object? value)
	{
		return value switch
		{
			RelativeDate relative => RuleWords.Relative(relative),
			DateTime day => RuleWords.Day(DateOnly.FromDateTime(day)),
			Duration duration => $"{duration.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {RuleWords.Short(duration.Unit)}",
			GenreMatch match => string.Join(", ", [.. match.Genres, .. match.Texts.Select(text => $"\"{text}\"")]),
			IEnumerable<string> picks => string.Join(", ", picks.Select(pick => Choice(field, pick))),
			double number => Number(field.Number, number),
			string text when field.Kind is FeedFieldKind.Choice => Choice(field, text),
			string text => $"\"{text}\"",
			_ => string.Empty,
		};
	}

	private static string Number(FeedNumberRule rule, double number)
	{
		var shown = number.ToString("#,0.##", CultureInfo.InvariantCulture);
		var unit = number is 1 ? rule.Unit : rule.Plural ?? rule.Unit;
		return unit is null ? shown : $"{shown} {unit}";
	}

	private static string Choice(FeedField field, string value)
	{
		return field.Options.FirstOrNone(option => option.Value == value).Map(option => option.Text).GetValueOr(value);
	}

	private static string Text(string? text) => Optional.Of(text).Map(known => known.Truncate(LongestText)).GetValueOr("none");

	private static string Count(FeedField field, int? count, MediaType type)
	{
		return (count, type) switch
		{
			({ } known, _) => Number(field.Number, known),
			(null, MediaType.Movie) => "none (a movie)",
			(null, _) => Unknown,
		};
	}

	private static string PerEpisode(int minutes) => TimeSpan.FromMinutes(minutes).Humanize(2, Notation.Symbols, TimeUnit.Minute);

	private static string InTotal(int minutes)
	{
		return $"{(minutes / 60.0).ToString("#,0.#", CultureInfo.InvariantCulture)} {RuleWords.Short(DurationUnit.Hours)}";
	}

	private static string By(SammlarrRating rating, RatingStrategy strategy)
	{
		return strategy switch
		{
			AveragedRating => "the average",
			OrderedRating => $"{RatingTexts.Name(rating.Counted[0].Source)} {rating.Counted[0].Value}, first in this feed's order",
		};
	}

	private static string Unvoted(SammlarrRating rating)
	{
		var silent = rating.Counted.Select(part => part.Source).Where(source => !source.Card.PublishesVotes).Distinct().Select(RatingTexts.Name).ToList();
		return silent.Count switch
		{
			0 => "no votes (no vote count came with its rating)",
			1 => $"no votes ({silent[0]} publishes none)",
			_ => $"no votes ({silent.Humanize(Notation.Words)} publish none)",
		};
	}
}
