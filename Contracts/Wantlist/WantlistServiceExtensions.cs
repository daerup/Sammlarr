namespace Contracts.Wantlist;

public static class WantlistServiceExtensions
{
	extension(IWantlistService)
	{
		public static int RecommendedConcurrency => 10;
	}
}
