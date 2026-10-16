using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

internal static class MediaFieldsGen
{
	private static readonly MediaField[] Details = [.. Enum.GetValues<MediaField>().Where(field => field is not MediaField.None)];

	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	public static Gen<MediaFields> Fields { get; } =
		Gen.Select(
			Subsets(Details.Length),
			Subsets(Sources.Length),
			(details, sources) => new MediaFields(Pick(Details, details).Aggregate(MediaField.None, (all, field) => all | field), [.. Pick(Sources, sources)])
		);

	public static Gen<ValueSet<RatingSource>> RatingSources { get; } =
		Subsets(Sources.Length).Select(sources => (ValueSet<RatingSource>)[.. Pick(Sources, sources)]);

	private static Gen<int> Subsets(int size) => Gen.OneOf(Gen.Const(0), Gen.Int[0, (1 << size) - 1]);

	private static IEnumerable<T> Pick<T>(T[] items, int mask) => items.Where((_, index) => (mask & (1 << index)) != 0);
}
