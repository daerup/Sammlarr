using Domain.Media;
using UI.Shared.Media.Ratings;

namespace UI.Tests.Media.Ratings;

public class RatingTextsTests
{
	[Test]
	[Arguments(RatingSource.RottenTomatoesCritics, "Rotten Tomatoes® critics", "RT critics")]
	[Arguments(RatingSource.RottenTomatoesAudience, "Rotten Tomatoes® audience", "RT audience")]
	[Arguments(RatingSource.Imdb, "IMDb", "IMDb")]
	public async Task Name_ForASource_CarriesRottenTomatoesRegisteredMarkWhileTheShortFormStaysPlain(RatingSource source, string name, string brief)
	{
		await Assert.That(RatingTexts.Name(source)).IsEqualTo(name);
		await Assert.That(RatingTexts.Short(source)).IsEqualTo(brief);
	}

	[Test]
	public async Task Scale_ForRottenTomatoesCritics_NamesItWithTheRegisteredMark()
	{
		await Assert.That(RatingTexts.Scale(RatingSource.RottenTomatoesCritics)).StartsWith("Rotten Tomatoes® critics: ");
	}
}
