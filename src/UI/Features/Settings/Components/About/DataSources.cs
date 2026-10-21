using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using UI.Features.Settings.Components.Housekeeping;
using UI.Features.Settings.Components.Providers.Roles;
using UI.Shared.Formatting;
using UI.Shared.Media;
using UI.Shared.Media.Ratings;
using UI.Shared.Services.Branding;

namespace UI.Features.Settings.Components.About;

internal sealed record SourceChip(string Icon, string Name, string Tip);

internal sealed record SourceRow(string Key, BrandingInfo Brand, IReadOnlyList<SourceChip> Chips, Optional<string> Via, string Home);

internal sealed class DataSources(
	IEnumerable<Keyed<RemoteService, IExternalIdConverter>> converters,
	IEnumerable<Keyed<RemoteService, IDetailsEnricher>> enrichers,
	MediaProviderBrandingService branding
)
{
	private const string PagesVia = "linked from a title; posters in Sonarr's search results";

	internal static readonly IReadOnlyList<RemoteService> Asked =
	[
		RemoteService.Tmdb,
		RemoteService.Plex,
		RemoteService.ImdbDatasets,
		RemoteService.Mdblist,
		RemoteService.Trakt,
		RemoteService.AniList,
		RemoteService.Mal,
		RemoteService.Tvmaze,
		RemoteService.Wikidata,
		RemoteService.AnimeLists,
	];

	internal static readonly IReadOnlyList<MediaProvider> Reached =
	[
		.. Enum.GetValues<MediaProvider>()
			.Except(Enum.GetValues<RemoteService>().Select(remote => remote.OwnIds).WhereSome())
			.OrderBy(provider => RatedOn(provider).Any()),
	];

	internal SourceRow Row(RemoteService remote)
	{
		IReadOnlyList<SourceChip> chips =
		[
			.. ServiceRoles.For(remote).Select(role => new SourceChip(role.Icon, role.Name, $"{role.Name}: {Lowered(role.Description)}")),
			.. new[] { Matching(remote), Details(remote), Ratings(remote.SuppliedRatingSources()) }.WhereSome(),
		];
		return new SourceRow(remote.ToString(), branding.GetBrandingInfo(remote), chips, new None(), Home(remote));
	}

	internal SourceRow Row(MediaProvider provider)
	{
		var rated = RatedOn(provider).ToList();
		var suppliers = rated.SelectMany(source => source.SuppliedBy()).Distinct().Order().Select(remote => branding.Name(remote)).ToList();
		var chip = Ratings(rated).GetValueOr(() => new SourceChip("link", "Pages", $"Pages: a title links to its page on {provider.Label}"));
		var via = suppliers.Count == 0 ? PagesVia : $"through {suppliers.Humanize(Notation.Words)}";
		return new SourceRow(provider.ToString(), branding.GetBrandingInfo(provider), [chip], Some.Of(via), Home(provider));
	}

	private Optional<SourceChip> Matching(RemoteService remote)
	{
		var clauses = converters.Where(converter => converter.Key == remote).SelectMany(converter => converter.Capability.Lookups).Select(Clause).ToList();
		return Optional.When(clauses.Count > 0, () => new SourceChip("link-2", "Matching", $"Matching: finds the same title {string.Join(", and ", clauses)}"));
	}

	private Optional<SourceChip> Details(RemoteService remote)
	{
		var fills = enrichers.Where(enricher => enricher.Key == remote).Aggregate(MediaField.None, (all, enricher) => all | enricher.Capability.Fills.Details);
		var fields = Enum.GetValues<MediaField>().Where(field => fills.Overlaps(field)).Select(Word).ToList();
		return Optional.When(fields.Count > 0, () => new SourceChip("file-text", "Details", $"Details: {fields.Humanize(Notation.Words)}"));
	}

	private static Optional<SourceChip> Ratings(IEnumerable<RatingSource> sources)
	{
		var names = sources.Order().Select(RatingTexts.Name).ToList();
		return Optional.When(names.Count > 0, () => new SourceChip("star", "Ratings", $"Ratings: {names.Humanize(Notation.Words)}"));
	}

	private static IEnumerable<RatingSource> RatedOn(MediaProvider provider) => Enum.GetValues<RatingSource>().Where(source => source.Card.Owner == provider);

	private static string Clause(IdLookup lookup)
	{
		return lookup switch
		{
			ById byId => $"on {Names(Found(byId), "and")} by its {Names(byId.Accepts, "or")} id",
			ByHints byHints => $"on {Names(byHints.Yields, "and")} by its title and year",
		};
	}

	private static IEnumerable<MediaProvider> Found(ById lookup) => lookup.Accepts.Count == 1 ? lookup.Yields.Except(lookup.Accepts) : lookup.Yields;

	private static string Names(IEnumerable<MediaProvider> providers, string conjunction)
	{
		return providers.Order().Select(provider => provider.Label).Humanize(Notation.Words, conjunction);
	}

	private static string Lowered(string text) => string.Concat(text[..1].ToLowerInvariant(), text[1..]);

	private static string Word(MediaField field)
	{
		return field switch
		{
			MediaField.None => string.Empty,
			MediaField.Title => "title",
			MediaField.Tagline => "tagline",
			MediaField.Summary => "summary",
			MediaField.Poster => "poster",
			MediaField.Backdrop => "backdrop",
			MediaField.ReleaseDate => "release date",
			MediaField.Genres => "genres",
			MediaField.Runtime => "runtime",
			MediaField.Seasons => "seasons",
			MediaField.Episodes => "episodes",
			MediaField.RuntimePerEpisode => "episode runtime",
		};
	}

	private static string Home(RemoteService remote)
	{
		return remote switch
		{
			RemoteService.ImdbDatasets or RemoteService.AnimeLists => HousekeepingView.Datasets[remote].Href,
			RemoteService.Wikidata => "https://www.wikidata.org",
			RemoteService.Radarr => "https://radarr.video",
			RemoteService.Sonarr => "https://sonarr.tv",
			RemoteService.Tmdb => Home(MediaProvider.Tmdb),
			RemoteService.Plex => Home(MediaProvider.Plex),
			RemoteService.Mdblist => Home(MediaProvider.Mdblist),
			RemoteService.Tvmaze => Home(MediaProvider.Tvmaze),
			RemoteService.AniList => Home(MediaProvider.AniList),
			RemoteService.Mal => Home(MediaProvider.Mal),
			RemoteService.Trakt => Home(MediaProvider.Trakt),
		};
	}

	private static string Home(MediaProvider provider)
	{
		return provider switch
		{
			MediaProvider.Tmdb => "https://www.themoviedb.org",
			MediaProvider.Imdb => "https://www.imdb.com",
			MediaProvider.RottenTomatoes => "https://www.rottentomatoes.com",
			MediaProvider.Tvdb => "https://thetvdb.com",
			MediaProvider.Plex => "https://www.plex.tv",
			MediaProvider.Mdblist => "https://mdblist.com",
			MediaProvider.Metacritic => "https://www.metacritic.com",
			MediaProvider.Letterboxd => "https://letterboxd.com",
			MediaProvider.Trakt => "https://trakt.tv",
			MediaProvider.Mal => "https://myanimelist.net",
			MediaProvider.Tvmaze => "https://www.tvmaze.com",
			MediaProvider.AniList => "https://anilist.co",
		};
	}
}
