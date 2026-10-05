using Domain.Media;

namespace UI.Shared.Services.Links;

internal sealed class NoPageLinker : IExternalPageLinker
{
	public Optional<string> PageOf(IReadOnlyCollection<IExternalMediaId> ids, Optional<string> title) => new None();
}
