using Domain.Identity;
using UI.Features.Settings.Components.About;
using UI.Features.Settings.Components.Diagnostics;
using UI.Features.Settings.Components.Housekeeping;
using UI.Features.Settings.Components.Notifications;
using UI.Features.Settings.Components.Providers;
using UI.Features.Settings.Components.Ratings;
using UI.Features.Settings.Components.Usage;
using UI.Features.Settings.Components.Users;
using UI.Shared.Branding;

namespace UI.Shared.Layout;

internal sealed record NavPage(string Label, string Href);

internal sealed record SettingsSection(string Label, string Icon, Type ComponentType, UserRole? RequiredRole = null, MagpieStarVariant? Mark = null);

internal static class AppSections
{
	internal static readonly IReadOnlyList<NavPage> Pages =
	[
		new("Browse", "/browse"),
		new("Feeds", "/feeds"),
		new("Wantlist", "/wantlist"),
		new("Blocklist", "/blocklist"),
	];

	private static readonly IReadOnlyList<SettingsSection> Settings =
	[
		new("Providers", "plug", typeof(ProvidersSection)),
		new("Ratings", "star", typeof(RatingsSection), Mark: MagpieStarVariant.Outline),
		new("Notifications", "bell", typeof(NotificationsSection)),
		new("Housekeeping", "database", typeof(HousekeepingSection), UserRole.Admin),
		new("Usage", "chart-column", typeof(UsageSection), UserRole.Admin),
		new("Diagnostics", "activity", typeof(DiagnosticsSection), UserRole.Admin),
		new("Users", "users", typeof(UsersSection), UserRole.Admin),
		new("Account", "user-round", typeof(AccountSection), UserRole.Member),
#if DEBUG
		new("Testing", "flask-conical", typeof(Features.Settings.Components.Testing.TestingSection), UserRole.Admin),
#endif
		new("About", "info", typeof(AboutSection)),
	];

	internal static IEnumerable<SettingsSection> SettingsFor(UserRole role)
	{
		return Settings.Where(section => section.RequiredRole is null || section.RequiredRole == role);
	}
}
