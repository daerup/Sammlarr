namespace Contracts.Media.Metadata;

public interface IMetadataEnricher
{
	Task<IPartialMedia?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}
