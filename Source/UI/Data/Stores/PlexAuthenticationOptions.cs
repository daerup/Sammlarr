namespace UI.Data.Stores;

public sealed class PlexAuthenticationOptions
{
	/// <summary>
	/// A stable UUID identifying this app instance to Plex.
	/// Generate once and persist in appsettings — no registration with Plex required.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Human-readable product name shown in the Plex auth screen.
	/// </summary>
	public string ProductName { get; set; } = "Sammlarr";
}
