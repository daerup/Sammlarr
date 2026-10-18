namespace Domain.Processing;

public sealed record GenreMatch(IReadOnlyList<string> Genres, IReadOnlyList<string> Texts)
{
	public static GenreMatch None { get; } = new([], []);

	public bool IsEmpty => Genres.Count == 0 && Texts.Count == 0;
}
