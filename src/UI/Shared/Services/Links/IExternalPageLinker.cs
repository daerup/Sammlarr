using Domain.Media;

namespace UI.Shared.Services.Links;

internal interface IExternalPageLinker
{
	Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title);
}
