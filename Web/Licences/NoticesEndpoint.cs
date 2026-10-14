namespace Web.Licences;

internal static class NoticesEndpoint
{
	private const string FileName = "THIRD-PARTY-NOTICES.txt";

	public static void MapNoticesEndpoint(this IEndpointRouteBuilder endpoints, string directory)
	{
		endpoints.MapGet($"/{FileName}", () => Serve(Path.Combine(directory, FileName))).RequireAuthorization();
	}

	private static IResult Serve(string path)
	{
		return File.Exists(path) ? Results.File(path, "text/plain; charset=utf-8", FileName) : Results.NotFound();
	}
}
