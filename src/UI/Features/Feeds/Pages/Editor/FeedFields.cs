using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using UI.Shared.Media;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedFields
{
	internal static IReadOnlyList<FeedField> All { get; } =
	[
		new(nameof(MediaQueryRow.Title), "Title", FilterFieldType.Text),
		new(nameof(MediaQueryRow.Tagline), "Tagline", FilterFieldType.Text),
		new(nameof(MediaQueryRow.Summary), "Summary", FilterFieldType.Text),
		new(nameof(MediaQueryRow.Genres), "Genres", FilterFieldType.Text),
		new(
			nameof(MediaQueryRow.MediaType),
			"Media Type",
			FilterFieldType.Enum,
			[new SelectOption<string>(nameof(MediaType.Movie), MediaType.Movie.Label), new SelectOption<string>(nameof(MediaType.Show), MediaType.Show.Label)]
		),
		new(nameof(MediaQueryRow.ReleaseDate), "Release Date", FilterFieldType.Date),
		new(nameof(MediaQueryRow.Rating), "Rating", FilterFieldType.Number),
		new(nameof(MediaQueryRow.Votes), "Votes", FilterFieldType.Number),
		new(nameof(MediaQueryRow.RuntimeMinutes), "Runtime (min)", FilterFieldType.Number),
		new(nameof(MediaQueryRow.Seasons), "Seasons", FilterFieldType.Number),
		new(nameof(MediaQueryRow.Episodes), "Episodes", FilterFieldType.Number),
	];

	internal static string LabelOf(string field) =>
		All.FirstOrNone(definition => definition.Property == field).Map(definition => definition.Label).GetValueOr(field);
}

internal sealed record FeedField(string Property, string Label, FilterFieldType FilterType, IReadOnlyList<SelectOption<string>>? Options = null);
