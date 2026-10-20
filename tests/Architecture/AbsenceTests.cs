namespace Architecture.Tests;

public class AbsenceTests
{
	[Test]
	public async Task Absence_OfNonPrivateMembers_IsOptional()
	{
		await Absence.NullableMembers.Where(member => !Edges.Allow(member)).Select(member => member.Describe()).AssertNone();
	}
}

internal static class Edges
{
	private static readonly string[] NotificationPayload = ["NotificationData", "NotificationMedia", "NotificationPayload"];

	private static readonly string[] Telemetry = ["Observability.Diagnostics", "DB.Observability"];

	private static readonly string[] MediaDetails = ["MediaDetails", "MovieDetails", "ShowDetails", "FieldChecks"];

	private static readonly string[] MediaDetailsFeeders = ["ProviderValues", "TmdbArtwork", "TmdbRatings"];

	private static readonly (string Type, string Member)[] Bridges =
	[
		("Optional", "Of"),
		("Result", "WhenNotNull"),
		("StringExtensions", "NullIfBlank"),
		("TypeExtensions", "GetGenericName"),
		("OptionalReferenceExtensions", "ToNullable"),
		("OptionalValueExtensions", "ToNullable"),
		("SensitiveModelBuilder", "Sensitive"),
		("ObservabilityContext", "ShouldUseOtlp"),
	];

	internal static bool Allow(NullableMember member)
	{
		var type = member.Type.Name;
		return type.EndsWith("Row")
			|| (member.Namespace.StartsWith("API.") && member.Namespace.EndsWith(".Types"))
			|| type is "FilterPredicate" or "FilterExpression"
			|| NotificationPayload.Contains(type)
			|| Telemetry.Contains(member.Namespace)
			|| (member.Kind is "constructor" && typeof(Exception).IsAssignableFrom(member.Type))
			|| member.Namespace is "API.Http"
			|| (member.Member.Name is "SendAsync" && member.Namespace.StartsWith("API.") && type.EndsWith("Client"))
			|| Bridges.Contains((type, member.Member.Name))
			|| (MediaDetails.Contains(type) && member.Namespace is "Domain.Media")
			|| MediaDetailsFeeders.Contains(type);
	}
}
