namespace API.Tmdb;

internal static class TmdbEndpoints
{
	public const string PageParam = "page";
	public const string MovieType = "movie";
	public const string ShowType = "tv";

	public const string Configuration = "configuration";
	public const string Detail = "{0}/{1}";
	public const string Find = "find/{0}?external_source={1}";
	public const string ExternalIds = "{0}/{1}/external_ids";
	public const string Trending = $"trending/all/week?{PageParam}={{0}}";
}
