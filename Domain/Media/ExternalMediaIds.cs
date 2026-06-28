namespace Domain.Media;

public static class ExternalMediaIds
{
	extension(IEnumerable<IExternalMediaId> externals)
	{
		public IExternalMediaId? OfProvider(MediaProvider provider) => externals.FirstOrDefault(id => id.Source == provider);
	}
}
