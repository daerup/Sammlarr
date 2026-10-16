using Contracts.Media.Artwork;

namespace Core.Tests.Media;

internal static class TestArtwork
{
	internal static IArtworkResizer ResizesAnything() => Resizing(_ => true);

	internal static IArtworkResizer Resizing(Func<string, bool> sizes)
	{
		var resizer = IArtworkResizer.Mock();
		resizer.Resize(Any(), Any()).Returns((url, _) => Optional.When(sizes(url), () => url));
		return resizer.Object;
	}
}
