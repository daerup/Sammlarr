using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Reqnroll;

namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class StepTransformers
{
	[StepArgumentTransformation]
	public MediaProvider ToMediaProvider(string raw) => MediaProviders.Parse(raw);

	public static MediaType ParseMediaKind(string raw) =>
		raw.Trim().ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
			_ => MediaType.Unknown,
		};
}
