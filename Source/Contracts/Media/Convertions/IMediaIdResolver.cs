namespace Contracts.Media.Convertions;

public interface IMediaIdResolver
{
	Task<ConversionResult> ResolveAsync(
		IEnumerable<IExternalMediaId> candidates,
		MediaProvider target,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	);
}
