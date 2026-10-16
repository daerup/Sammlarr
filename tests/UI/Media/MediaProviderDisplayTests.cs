using Domain.Providers;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaProviderDisplayTests
{
	[Test]
	[Arguments(MediaProvider.RottenTomatoes, "Rotten Tomatoes®")]
	[Arguments(MediaProvider.Tmdb, "TMDB")]
	[Arguments(MediaProvider.Tvdb, "TVDB")]
	[Arguments(MediaProvider.Mal, "MyAnimeList")]
	public async Task Label_ForAProvider_IsItsNameWithRottenTomatoesRegisteredMark(MediaProvider provider, string label)
	{
		await Assert.That(provider.Label).IsEqualTo(label);
	}
}
