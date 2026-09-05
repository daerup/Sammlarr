using System.Globalization;
using Microsoft.AspNetCore.Builder;

namespace Web;

internal static class BrowserLocalization
{
	public static RequestLocalizationOptions CreateOptions()
	{
		var supportedCultures = CultureInfo
			.GetCultures(CultureTypes.AllCultures)
			.Where(culture => !string.IsNullOrWhiteSpace(culture.Name))
			.Select(culture => culture.Name)
			.ToArray();

		var options = new RequestLocalizationOptions();
		options.SetDefaultCulture("en-US");
		options.AddSupportedCultures(supportedCultures);
		options.AddSupportedUICultures(supportedCultures);
		return options;
	}
}
