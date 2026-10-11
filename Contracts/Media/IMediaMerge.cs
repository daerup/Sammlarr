using Domain.Media;

namespace Contracts.Media;

public interface IMediaMerge
{
	Task<MergeOutcome> MergeAsync(MediaId held, MediaId other, CancellationToken cancellationToken = default);
}
