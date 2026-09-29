using Domain.Providers;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ProviderCaptionTests
{
	[Test]
	[Arguments(MediaProvider.Plex, "Account · search, watchlist, sign-in")]
	[Arguments(MediaProvider.Tmdb, "API key · trending, ratings, feeds")]
	[Arguments(MediaProvider.Mdblist, "API key · feeds")]
	public async Task For_AProvider_NamesItsCredentialThenEveryOtherCapability(MediaProvider provider, string caption)
	{
		await Assert.That(ProviderCaption.For(provider)).IsEqualTo(caption);
	}
}
