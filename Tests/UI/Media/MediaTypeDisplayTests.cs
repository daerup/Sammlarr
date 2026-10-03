using Domain.Media;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaTypeDisplayTests
{
	[Test]
	[Arguments(MediaType.Movie, "film", "Movie", "Movies")]
	[Arguments(MediaType.Show, "tv", "Show", "Shows")]
	[Arguments(MediaType.Unknown, "circle-help", "Unknown", "Unknown")]
	public async Task Display_ForEachType_NamesItsIconLabelAndPlural(MediaType type, string icon, string label, string plural)
	{
		await Assert.That(type.Icon).IsEqualTo(icon);
		await Assert.That(type.Label).IsEqualTo(label);
		await Assert.That(type.Plural).IsEqualTo(plural);
	}
}
