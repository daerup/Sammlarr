namespace Contracts.Media.Convertions;

public interface IMediaIdResolver
{
	Task<ConversionResult> ResolveAsync(
		IReadOnlyCollection<IMediaId> candidates,
		MediaProvider target,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
