namespace Contracts.Media;

public static class MediaServiceExtensions
{
	extension(IMediaService)
	{
		public static int RecommendedConcurrency => 100;
	}
}
