namespace Domain.Media;

public interface IListItem<out TMedia>
{
	MediaId MediaId { get; }

	TMedia With(IdentifiedMedia media);
}
