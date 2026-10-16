using Shared.Options;

namespace Web.Auth;

internal static class PublicOrigin
{
	internal const string Requirement =
		"Sammlarr__PublicUrl must be empty or the http or https root address people open Sammlarr at, like https://sammlarr.example.com.";

	internal static string Of(SammlarOptions options, HttpRequest request)
	{
		var configured = Configured(options);
		return configured.Length > 0 ? configured : $"{request.Scheme}://{request.Host}";
	}

	internal static bool IsValid(SammlarOptions options)
	{
		var configured = Configured(options);
		return configured.Length == 0 || IsRoot(configured);
	}

	private static bool IsRoot(string url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out var uri)
			&& uri.Scheme is "http" or "https"
			&& uri.PathAndQuery == "/"
			&& uri.Fragment.Length == 0
			&& uri.UserInfo.Length == 0;
	}

	private static string Configured(SammlarOptions options) => options.PublicUrl.Trim().TrimEnd('/');
}
