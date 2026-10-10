namespace AppHost.Staging;

internal sealed record StagingLayout(string Root)
{
	internal string Sammlarr => Path.Combine(Root, "sammlarr");

	internal string Database => Path.Combine(Sammlarr, "sammlarr.db");

	internal string KeyRing => Path.Combine(Sammlarr, "dp-keys");

	internal string Downloads => Path.Combine(Root, "downloads");

	internal string Catcher => Path.Combine(Root, "catcher");

	internal string ConfigOf(ArrContainers.ArrContainer app) => Path.Combine(Root, app.Name, "config");

	internal string LibraryOf(ArrContainers.ArrContainer app) => Path.Combine(Root, app.Name, app.Library.TrimStart('/'));

	internal string AnimeLibraryOf(ArrContainers.ArrContainer app) => Path.Combine(Root, app.Name, app.AnimeLibrary.TrimStart('/'));

	internal string DownloadsOf(ArrContainers.ArrContainer app) => Path.Combine(Downloads, app.Name);

	internal static StagingLayout From(string? configured)
	{
		var root = configured ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sammlarr", "staging");
		var layout = new StagingLayout(Path.GetFullPath(root));
		layout.Ensure();
		return layout;
	}

	internal void Wipe()
	{
		string[] wiped = [Sammlarr, Path.Combine(Root, ArrContainers.Radarr.Name), Path.Combine(Root, ArrContainers.Sonarr.Name), Downloads];
		foreach (var directory in wiped.Where(Directory.Exists))
		{
			Directory.Delete(directory, recursive: true);
		}

		Ensure();
	}

	private void Ensure()
	{
		Directory.CreateDirectory(Sammlarr);
		Directory.CreateDirectory(Catcher);
		foreach (var app in new[] { ArrContainers.Radarr, ArrContainers.Sonarr })
		{
			Directory.CreateDirectory(ConfigOf(app));
			Directory.CreateDirectory(LibraryOf(app));
			Directory.CreateDirectory(AnimeLibraryOf(app));
			Directory.CreateDirectory(Path.Combine(DownloadsOf(app), "nzb"));
			Directory.CreateDirectory(Path.Combine(DownloadsOf(app), "watch"));
		}
	}
}

internal sealed class StagingScenarioChoice(string initial)
{
	internal const string Configured = "Configured";

	internal const string Fresh = "Fresh";

	internal string Value { get; set; } = initial;
}
