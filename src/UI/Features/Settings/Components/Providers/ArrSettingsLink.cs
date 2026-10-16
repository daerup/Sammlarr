namespace UI.Features.Settings.Components.Providers;

internal static class ArrSettingsLink
{
	internal static Optional<string> For(string url)
	{
		return Optional.When(
			Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
			() => $"{uri!.GetLeftPart(UriPartial.Path).TrimEnd('/')}/settings/general"
		);
	}
}
