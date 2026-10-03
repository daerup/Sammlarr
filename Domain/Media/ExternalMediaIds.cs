using Domain.Providers;

namespace Domain.Media;

public static class ExternalMediaIds
{
	extension(IEnumerable<IExternalMediaId> externals)
	{
		public Optional<IExternalMediaId> OfProvider(MediaProvider provider) => externals.FirstOrNone(id => id.Source == provider);
	}
}
