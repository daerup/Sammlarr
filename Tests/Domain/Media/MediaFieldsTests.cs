using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

public class MediaFieldsTests
{
	private static readonly MediaField[] Details = [.. Enum.GetValues<MediaField>().Where(field => field is not MediaField.None)];

	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	private static readonly Gen<MediaFields> Fields = Gen.Select(
		Subsets(Details.Length),
		Subsets(Sources.Length),
		(details, sources) => new MediaFields(Pick(Details, details).Aggregate(MediaField.None, (all, field) => all | field), [.. Pick(Sources, sources)])
	);

	[Test]
	public async Task Rated_HoldsExactlyTheSourcesGivenAndNoDetail()
	{
		var rated = MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);

		await Assert.That(rated.Details).IsEqualTo(MediaField.None);
		await Assert.That(rated.Ratings).IsEquivalentTo([RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);
	}

	[Test]
	public async Task ToString_NamesTheDetailsThenTheSources()
	{
		var fields = MediaField.Tagline | MediaFields.Rated([RatingSource.RottenTomatoesAudience, RatingSource.Imdb]);

		await Assert.That(fields.ToString()).IsEqualTo("Tagline, Imdb, RottenTomatoesAudience");
	}

	[Test]
	public async Task ToString_WhenEmpty_IsNone()
	{
		await Assert.That(MediaFields.None.ToString()).IsEqualTo("None");
	}

	[Test]
	public async Task Names_OfDetailsAndRatingSources_NeverClash()
	{
		await Assert.That(Enum.GetNames<MediaField>().Intersect(Enum.GetNames<RatingSource>())).IsEmpty();
	}

	[Test]
	public void Or_ForAnyTwo_HoldsEveryFieldOfBothAndNothingElse()
	{
		Gen.Select(Fields, Fields)
			.Sample((left, right) => left.Except(left | right).IsEmpty && right.Except(left | right).IsEmpty && (left | right).Except(left).Except(right).IsEmpty);
	}

	[Test]
	public void ExceptAndAnd_ForAnyTwo_SplitTheLeftIntoWhatTheRightLacksAndWhatBothHave()
	{
		Gen.Select(Fields, Fields).Sample((left, right) => (left.Except(right) | (left & right)) == left && (left.Except(right) & right).IsEmpty);
	}

	[Test]
	public void And_ForAnyTwo_HoldsOnlyWhatBothHave()
	{
		Gen.Select(Fields, Fields).Sample((left, right) => (left & right).Except(left).IsEmpty && (left & right).Except(right).IsEmpty);
	}

	[Test]
	public void Overlaps_ForAnyTwo_IsANonEmptyIntersection()
	{
		Gen.Select(Fields, Fields).Sample((left, right) => left.Overlaps(right) == !(left & right).IsEmpty);
	}

	[Test]
	public void Count_ForAnyTwo_CountsTheUnionAndTheIntersectionLikeBothTogether()
	{
		Gen.Select(Fields, Fields).Sample((left, right) => (left | right).Count + (left & right).Count == left.Count + right.Count);
	}

	[Test]
	public void IsEmpty_ForAny_MeansNoFieldAtAll()
	{
		Fields.Sample(fields => fields.IsEmpty == (fields.Count == 0) && fields.IsEmpty == (fields == MediaFields.None));
	}

	[Test]
	public void Parse_ForAny_ReadsBackWhatToStringWrote()
	{
		Fields.Sample(fields => MediaFields.Parse(fields.ToString()) == fields);
	}

	private static Gen<int> Subsets(int size) => Gen.OneOf(Gen.Const(0), Gen.Int[0, (1 << size) - 1]);

	private static IEnumerable<T> Pick<T>(T[] items, int mask) => items.Where((_, index) => (mask & (1 << index)) != 0);
}
