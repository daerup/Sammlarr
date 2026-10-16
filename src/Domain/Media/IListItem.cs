namespace Domain.Media;

public interface IListItem<out TMedia>
{
	MediaId MediaId { get; }

	TMedia With(MatchedMedia media);
}
