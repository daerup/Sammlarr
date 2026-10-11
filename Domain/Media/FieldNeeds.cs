namespace Domain.Media;

public readonly record struct FieldNeeds(MediaFields Required, MediaFields Shown)
{
	public static FieldNeeds None { get; } = new(MediaFields.None, MediaFields.None);

	public static FieldNeeds Everything { get; } = new(MediaFields.Everything, MediaFields.None);

	public MediaFields All => Required | Shown;

	public bool IsEmpty => All.IsEmpty;

	public FieldNeeds Of(MediaFields missing) => new(Required & missing, (Shown & missing).Except(Required));
}
