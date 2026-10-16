using System.Text.Json;
using System.Text.Json.Serialization;

namespace UI.Features.Settings.Components.About;

[JsonConverter(typeof(JsonStringEnumConverter<LogoOrigin>))]
internal enum LogoOrigin
{
	Selfhst,
	SimpleIcons,
	WikimediaCommons,
	Owner,
	Sammlarr,
}

internal sealed record LogoCredit(string File, LogoOrigin Origin, IReadOnlyList<string> Sources, string Author, string Licence, string Changes);

internal static class LogoCredits
{
	internal static readonly IReadOnlyList<LogoCredit> All = Load();

	internal static IEnumerable<IGrouping<LogoOrigin, LogoCredit>> ByOrigin => All.GroupBy(credit => credit.Origin).OrderBy(group => group.Key);

	private static IReadOnlyList<LogoCredit> Load()
	{
		using var credits = typeof(LogoCredits).Assembly.GetManifestResourceStream("UI.providers.credits.json")!;
		return JsonSerializer.Deserialize<IReadOnlyList<LogoCredit>>(credits, JsonSerializerOptions.Web)!;
	}
}
