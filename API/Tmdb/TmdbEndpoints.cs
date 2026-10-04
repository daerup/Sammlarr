namespace API.Tmdb;

internal static class TmdbEndpoints
{
	public const string PageParam = "page";
	public const string MovieType = "movie";
	public const string ShowType = "tv";

	public const string Configuration = "configuration";
	public const string Detail = "{0}/{1}";
	public const string DetailWithExternalIds = "{0}/{1}?append_to_response=external_ids";
	public const string Find = "find/{0}?external_source={1}";
	public const string Trending = $"trending/all/week?{PageParam}={{0}}";
	public const string Search = "search/multi?query={0}&include_adult=false";
}
