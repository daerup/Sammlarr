namespace Domain.Media;

public readonly record struct MediaFields(MediaField Details, ValueSet<RatingSource> Ratings)
{
	private const string Nothing = "None";

	private const string Separator = ", ";

	public static MediaFields None => default;

	public static MediaFields AllRatings { get; } = Rated(Enum.GetValues<RatingSource>());

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
			.Select(name => Enum.TryParse<MediaField>(name, out var detail) ? detail : Rated([Enum.Parse<RatingSource>(name)]))
			.Aggregate(None, (fields, field) => fields | field);
	}
}
