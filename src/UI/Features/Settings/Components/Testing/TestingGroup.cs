namespace UI.Features.Settings.Components.Testing;

public sealed record TestingGroup(string Name, string Icon)
{
	internal string Id => $"testing-{string.Concat(Name.TakeWhile(char.IsLetter)).ToLowerInvariant()}";

	internal static readonly TestingGroup Branding = new("Branding", "palette");

	internal static readonly TestingGroup People = new("People", "users");

	internal static readonly TestingGroup Ratings = new("Ratings", "star");

	internal static readonly TestingGroup Providers = new("Providers and sinks", "plug");

	internal static readonly TestingGroup Setup = new("Setup", "wand-sparkles");

	internal static readonly TestingGroup Common = new("Feeds, home and common", "layout-grid");

	internal static readonly IReadOnlyList<TestingGroup> All = [Branding, People, Ratings, Providers, Setup, Common];
}
