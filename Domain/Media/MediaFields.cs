namespace Domain.Media;

public readonly record struct MediaFields(MediaField Details, ValueSet<RatingSource> Ratings)
{
	private const string Nothing = "None";

	private const string Separator = ", ";

	public static MediaFields None => default;

	public static MediaFields AllRatings { get; } = Rated(Enum.GetValues<RatingSource>());

	public static MediaFields Everything { get; } = Enum.GetValues<MediaField>().Aggregate(MediaField.None, (all, field) => all | field) | AllRatings;

	public bool IsEmpty => Details is MediaField.None && Ratings.Count == 0;

	public int Count => Details.Count + Ratings.Count;

	public int DetailCount => Details.Count;

	public static MediaFields Rated(IEnumerable<RatingSource> sources) => new(MediaField.None, [.. sources]);

	public bool Overlaps(MediaFields other) => Details.Overlaps(other.Details) || Ratings.Overlaps(other.Ratings);

	public MediaFields Except(MediaFields other) => new(Details.Except(other.Details), [.. Ratings.Where(source => !other.Ratings.Contains(source))]);

	public static implicit operator MediaFields(MediaField details) => new(details, []);

	public static MediaFields operator |(MediaFields left, MediaFields right) => new(left.Details | right.Details, left.Ratings + right.Ratings);

	public static MediaFields operator &(MediaFields left, MediaFields right)
	{
		return new(left.Details & right.Details, [.. left.Ratings.Where(right.Ratings.Contains)]);
	}

	public override string ToString()
	{
		string[] details = Details is MediaField.None ? [] : Details.ToString().Split(Separator);
		string[] names = [.. details, .. Enum.GetValues<RatingSource>().Where(Ratings.Contains).Select(source => source.ToString())];
		return names.Length == 0 ? Nothing : string.Join(Separator, names);
	}

	public static MediaFields Parse(string text)
	{
		return text.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
			.Where(name => name != Nothing)
			.Select(Field)
			.WhereSome()
			.Aggregate(None, (fields, field) => fields | field);
	}

	private static Optional<MediaFields> Field(string name)
	{
		return Known<MediaField>(name).Map(detail => (MediaFields)detail).Or(() => Known<RatingSource>(name).Map(source => Rated([source])));
	}

	private static Optional<T> Known<T>(string name)
		where T : struct, Enum
	{
		return Optional.When(Enum.TryParse<T>(name, out var value) && Enum.IsDefined(value), () => value);
	}
}
