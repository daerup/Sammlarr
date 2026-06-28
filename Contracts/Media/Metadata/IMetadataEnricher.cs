namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<MediaLookup?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
