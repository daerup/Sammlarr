using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using UI.Shared.Media;
using Duration = Domain.Processing.Duration;
using DurationUnit = Domain.Processing.DurationUnit;

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
		new(FilterOperator.In, "contains any"),
		new(FilterOperator.Contains, "contains all"),
		new(FilterOperator.NotIn, "contains none"),
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
		new(nameof(MediaQueryRow.Genres), "Genres", FeedFieldKind.Genres, GenreOperators),
		new(nameof(MediaQueryRow.MediaType), "Media Type", FeedFieldKind.Choice, ChoiceOperators)
		{
			Options =
			[
				new SelectOption<string>(nameof(MediaType.Movie), MediaType.Movie.Label),
				new SelectOption<string>(nameof(MediaType.Show), MediaType.Show.Label),
			],
		},
		new(nameof(MediaQueryRow.ReleaseDate), "Release Date", FeedFieldKind.Date, DateOperators) { Sortable = true },
		new(nameof(MediaQueryRow.Rating), "Rating", FeedFieldKind.Number, NumberOperators)
		{
			Number = new FeedNumberRule(Min: 0, Max: 10, Step: 0.1, Unit: "/ 10"),
			Sortable = true,
		},
		new(nameof(MediaQueryRow.Votes), "Votes", FeedFieldKind.Number, NumberOperators) { Number = new FeedNumberRule(Min: 0, Step: 100), Sortable = true },
		new(nameof(MediaQueryRow.RuntimeMinutes), "Runtime", FeedFieldKind.Runtime, NumberOperators)
		{
			Number = Count,
			Total = Some.Of(nameof(MediaQueryRow.TotalRuntimeMinutes)),
			Sortable = true,
		},
		new(nameof(MediaQueryRow.Seasons), "Seasons", FeedFieldKind.Number, NumberOperators)
		{
			Number = Count with { Unit = "season", Plural = "seasons" },
			ShowsOnly = true,
			Sortable = true,
		},
		new(nameof(MediaQueryRow.Episodes), "Episodes", FeedFieldKind.Number, NumberOperators)
		{
			Number = Count with { Unit = "episode", Plural = "episodes" },
			ShowsOnly = true,
			Sortable = true,
		},
	];

	internal static IReadOnlyList<SelectOption<string>> SortOptions { get; } =
	[.. All.Where(field => field.Sortable).Select(field => new SelectOption<string>(field.Property, field.Label))];

	internal static FeedField For(string field) => All.Single(definition => definition.Names(field));

	internal static Optional<FeedField> Of(string field) => All.FirstOrNone(definition => definition.Names(field));

	internal static string LabelOf(string field) => Of(field).Map(definition => definition.Label).GetValueOr(field);

	internal static bool TakesPicks(FilterCondition condition)
	{
		return condition.Operator is FilterOperator.In or FilterOperator.NotIn || condition.Field == nameof(MediaQueryRow.Genres);
	}

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
	Genres,
	Runtime,
}

internal sealed record FeedField(string Property, string Label, FeedFieldKind Kind, IReadOnlyList<SelectOption<FilterOperator>> Operators)
{
	public IReadOnlyList<SelectOption<string>> Options { get; init; } = [];

	public FeedNumberRule Number { get; init; } = new();

	public Optional<string> Total { get; init; } = new None();

	public bool ShowsOnly { get; init; }

	public bool Sortable { get; init; }

	public object? Blank
	{
		get
		{
			return Kind switch
			{
				FeedFieldKind.Number => 0.0,
				FeedFieldKind.Runtime => new Duration(0, DurationUnit.Minutes),
				FeedFieldKind.Text or FeedFieldKind.Choice or FeedFieldKind.Date or FeedFieldKind.Genres => null,
			};
		}
	}

	public bool Names(string field) => Property == field || IsTotal(field);

	public bool IsTotal(string field) => Total.Match(total => total == field, () => false);

	public string NameFor(bool total) => total ? Total.GetValueOr(Property) : Property;

	public bool Offers(FilterOperator op) => Operators.Any(offered => offered.Value == op);
}

public sealed record FeedNumberRule(double? Min = null, double? Max = null, double? Step = null, string? Unit = null, string? Plural = null);
