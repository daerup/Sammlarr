namespace Contracts.Media.Convertions;

public interface IMediaIdResolver
{
	Task<ResolvedId> ResolveAsync(IMediaId sourceId, MediaProvider target);
}
