using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using UI.Shared.Media;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedFields
{
	private static readonly IReadOnlyList<SelectOption<FilterOperator>> TextOperators =
	[
		new(FilterOperator.Equals, "equals"),
		new(FilterOperator.NotEquals, "not equals"),
		new(FilterOperator.Contains, "contains"),
		new(FilterOperator.NotContains, "not contains"),
		new(FilterOperator.StartsWith, "starts with"),
		new(FilterOperator.EndsWith, "ends with"),
	];

	private static readonly IReadOnlyList<SelectOption<FilterOperator>> GenreOperators =
	[
		new(FilterOperator.Contains, "contains"),
		new(FilterOperator.NotContains, "not contains"),
	];

	private static readonly IReadOnlyList<SelectOption<FilterOperator>> ChoiceOperators =
	[
		new(FilterOperator.Equals, "equals"),
		new(FilterOperator.NotEquals, "not equals"),
		new(FilterOperator.In, "is any of"),
		new(FilterOperator.NotIn, "is none of"),
	];

	private static readonly IReadOnlyList<SelectOption<FilterOperator>> DateOperators =
	[
		new(FilterOperator.Equals, "equals"),
		new(FilterOperator.NotEquals, "not equals"),
		new(FilterOperator.GreaterThan, "is after"),
		new(FilterOperator.LessThan, "is before"),
		new(FilterOperator.Between, "between"),
	];

	private static readonly IReadOnlyList<SelectOption<FilterOperator>> NumberOperators =
	[
		new(FilterOperator.Equals, "equals"),
		new(FilterOperator.NotEquals, "not equals"),
		new(FilterOperator.GreaterThan, "greater than"),
		new(FilterOperator.LessThan, "less than"),
		new(FilterOperator.GreaterOrEqual, "greater or equal"),
		new(FilterOperator.LessOrEqual, "less or equal"),
		new(FilterOperator.Between, "between"),
	];

	private static readonly FeedNumberRule Count = new(Min: 0, Step: 1);

	internal static IReadOnlyList<FeedField> All { get; } =
	[
		new(nameof(MediaQueryRow.Title), "Title", FeedFieldKind.Text, TextOperators),
		new(nameof(MediaQueryRow.Tagline), "Tagline", FeedFieldKind.Text, TextOperators),
		new(nameof(MediaQueryRow.Summary), "Summary", FeedFieldKind.Text, TextOperators),
		new(nameof(MediaQueryRow.Genres), "Genres", FeedFieldKind.Text, GenreOperators),
		new(nameof(MediaQueryRow.MediaType), "Media Type", FeedFieldKind.Choice, ChoiceOperators)
		{
			Options =
			[
				new SelectOption<string>(nameof(MediaType.Movie), MediaType.Movie.Label),
				new SelectOption<string>(nameof(MediaType.Show), MediaType.Show.Label),
			],
		},
		new(nameof(MediaQueryRow.ReleaseDate), "Release Date", FeedFieldKind.Date, DateOperators),
		new(nameof(MediaQueryRow.Rating), "Rating", FeedFieldKind.Number, NumberOperators)
		{
			Number = new FeedNumberRule(Min: 0, Max: 10, Step: 0.1, Unit: "/ 10"),
		},
		new(nameof(MediaQueryRow.Votes), "Votes", FeedFieldKind.Number, NumberOperators) { Number = new FeedNumberRule(Min: 0, Step: 100) },
		new(nameof(MediaQueryRow.RuntimeMinutes), "Runtime (min)", FeedFieldKind.Number, NumberOperators) { Number = Count },
		new(nameof(MediaQueryRow.Seasons), "Seasons", FeedFieldKind.Number, NumberOperators) { Number = Count },
		new(nameof(MediaQueryRow.Episodes), "Episodes", FeedFieldKind.Number, NumberOperators) { Number = Count },
	];

	internal static IReadOnlyList<SelectOption<string>> Options { get; } = [.. All.Select(field => new SelectOption<string>(field.Property, field.Label))];

	internal static FeedField For(string field) => All.Single(definition => definition.Property == field);

	internal static string LabelOf(string field) =>
		All.FirstOrNone(definition => definition.Property == field).Map(definition => definition.Label).GetValueOr(field);

	internal static FilterCondition NewCondition()
	{
		var first = All[0];
		return new FilterCondition { Field = first.Property, Operator = first.Operators[0].Value };
	}
}

internal enum FeedFieldKind
{
	Text,
	Number,
	Choice,
	Date,
}

internal sealed record FeedField(string Property, string Label, FeedFieldKind Kind, IReadOnlyList<SelectOption<FilterOperator>> Operators)
{
	public IReadOnlyList<SelectOption<string>> Options { get; init; } = [];

	public FeedNumberRule Number { get; init; } = new();

	public bool Offers(FilterOperator op) => Operators.Any(offered => offered.Value == op);
}

public sealed record FeedNumberRule(double? Min = null, double? Max = null, double? Step = null, string? Unit = null);
