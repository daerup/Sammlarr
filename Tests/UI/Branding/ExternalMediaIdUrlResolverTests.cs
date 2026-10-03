using Domain.Media;
using UI.Shared.Services.Branding;

namespace UI.Tests.Branding;

public class ExternalMediaIdUrlResolverTests
{
	[Test]
	public async Task Resolve_ForATmdbPersonFromTrending_LinksToTheirTmdbPage()
	{
		var url = ExternalMediaIdUrlResolver.Resolve(TmdbId.From("287", MediaType.Unknown));

		await Assert.That(url).IsEqualTo("https://www.themoviedb.org/person/287");
	}
}
