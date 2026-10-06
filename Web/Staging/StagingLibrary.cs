using System.Text.Json.Nodes;

namespace Web.Staging;

internal static class StagingLibrary
{
	private const string Quality = "WEBDL-1080p";

	private static readonly char[] Unsafe = [.. Path.GetInvalidFileNameChars(), ':'];

	internal static async Task FillRadarrAsync(
		ArrClient radarr,
		ArrAppOptions options,
		int profileId,
		IReadOnlyList<LibraryTitle> titles,
		CancellationToken cancellationToken
	)
	{
		var held = await HeldAsync(radarr, "movie", "tmdbId", cancellationToken);
		foreach (var title in titles.Where(title => !held.Contains(title.Id)))
		{
			await AddMovieAsync(radarr, title, await TagsAsync(radarr, title, cancellationToken), options, profileId, cancellationToken);
		}
	}

	internal static async Task FillSonarrAsync(
		ArrClient sonarr,
		ArrAppOptions options,
		int profileId,
		IReadOnlyList<LibraryTitle> titles,
		CancellationToken cancellationToken
	)
	{
		var held = await HeldAsync(sonarr, "series", "tvdbId", cancellationToken);
		foreach (var title in titles.Where(title => !held.Contains(title.Id)))
		{
			await AddSeriesAsync(sonarr, title, await TagsAsync(sonarr, title, cancellationToken), options, profileId, cancellationToken);
		}
	}

	private static async Task<int[]> TagsAsync(ArrClient arr, LibraryTitle title, CancellationToken cancellationToken)
	{
		return title.Owner is { } owner ? [await TagAsync(arr, $"sammlarr-u-{owner}", cancellationToken)] : [];
	}

	private static async Task<int> TagAsync(ArrClient arr, string label, CancellationToken cancellationToken)
	{
		var existing = (await arr.GetAsync("tag", cancellationToken)).AsArray().FirstOrDefault(tag => tag!["label"]!.GetValue<string>() == label);
		var tag = existing ?? await arr.PostAsync("tag", new { label }, cancellationToken);
		return tag["id"]!.GetValue<int>();
	}

	private static async Task<HashSet<int>> HeldAsync(ArrClient arr, string library, string idProperty, CancellationToken cancellationToken)
	{
		return [.. (await arr.GetAsync(library, cancellationToken)).AsArray().Select(title => title![idProperty]!.GetValue<int>())];
	}

	private static async Task AddMovieAsync(
		ArrClient radarr,
		LibraryTitle title,
		int[] tags,
		ArrAppOptions options,
		int profileId,
		CancellationToken cancellationToken
	)
	{
		var movie = (await radarr.GetAsync($"movie/lookup?term=tmdb:{title.Id}", cancellationToken))[0]!;
		var folder = Folder(movie);
		if (title.OnDisk)
		{
			Place(Path.Combine(options.Library, folder), $"{folder} {Quality}.mkv");
		}

		await radarr.PostAsync(
			"movie",
			new
			{
				title = movie["title"]!.GetValue<string>(),
				tmdbId = title.Id,
				year = movie["year"]!.GetValue<int>(),
				qualityProfileId = profileId,
				path = $"{options.RootFolder}/{folder}",
				monitored = true,
				minimumAvailability = "released",
				tags,
				addOptions = new { searchForMovie = false },
			},
			cancellationToken
		);
	}

	private static async Task AddSeriesAsync(
		ArrClient sonarr,
		LibraryTitle title,
		int[] tags,
		ArrAppOptions options,
		int profileId,
		CancellationToken cancellationToken
	)
	{
		var series = (await sonarr.GetAsync($"series/lookup?term=tvdb:{title.Id}", cancellationToken))[0]!;
		var folder = Folder(series);
		if (title.OnDisk)
		{
			Place(Path.Combine(options.Library, folder, "Season 01"), $"{Clean(series["title"]!.GetValue<string>())} - S01E01 {Quality}.mkv");
		}

		await sonarr.PostAsync(
			"series",
			new
			{
				title = series["title"]!.GetValue<string>(),
				tvdbId = title.Id,
				qualityProfileId = profileId,
				path = $"{options.RootFolder}/{folder}",
				seasonFolder = true,
				monitored = true,
				seriesType = "standard",
				seasons = series["seasons"]!.DeepClone(),
				tags,
				addOptions = new { monitor = "all", searchForMissingEpisodes = false },
			},
			cancellationToken
		);
	}

	private static void Place(string directory, string file)
	{
		Directory.CreateDirectory(directory);
		File.WriteAllBytes(Path.Combine(directory, file), []);
	}

	private static string Folder(JsonNode title) => $"{Clean(title["title"]!.GetValue<string>())} ({title["year"]!.GetValue<int>()})";

	private static string Clean(string title)
	{
		return string.Join(' ', new string([.. title.Where(character => !Unsafe.Contains(character))]).Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}
}
