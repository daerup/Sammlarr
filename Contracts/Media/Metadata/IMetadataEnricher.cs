namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<MediaMatch?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
