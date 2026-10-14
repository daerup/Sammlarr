namespace Web.Licences;

internal static class NoticesEndpoint
{
	private const string FileName = "THIRD-PARTY-NOTICES.txt";

	public static void MapNoticesEndpoint(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet($"/{FileName}", () => Serve(AppContext.BaseDirectory));
	}

	internal static IResult Serve(string directory)
	{
		var path = Path.Combine(directory, FileName);
		return File.Exists(path) ? Results.File(path, "text/plain; charset=utf-8", FileName) : Results.NotFound();
	}
}
