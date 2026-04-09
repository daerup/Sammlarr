namespace Contracts.Media.Convertions;

public interface IMediaIdResolver
{
	Task<ResolvedId> ResolveAsync<TMediaId>(TMediaId sourceId, MediaProvider target)
		where TMediaId : struct, IMediaId;
}
