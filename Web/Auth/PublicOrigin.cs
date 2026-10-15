using Shared.Options;

namespace Web.Auth;

internal static class PublicOrigin
{
	internal static string Of(SammlarOptions options, HttpRequest request)
	{
		var configured = options.PublicUrl.Trim().TrimEnd('/');
		return configured.Length > 0 ? configured : $"{request.Scheme}://{request.Host}";
	}
}
