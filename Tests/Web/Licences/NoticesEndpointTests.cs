using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Web.Licences;

namespace Web.Tests.Licences;

public class NoticesEndpointTests
{
	private readonly string _directory = Directory.CreateTempSubdirectory("sammlarr-notices-").FullName;

	[Test]
	public async Task Serve_InAPublishedApp_SendsTheNoticesAsATextDownload()
	{
		await File.WriteAllTextAsync(Path.Combine(_directory, "THIRD-PARTY-NOTICES.txt"), "Cronos 0.11.1 MIT");
		var context = Request();

		await NoticesEndpoint.Serve(_directory).ExecuteAsync(context);

		await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
		await Assert.That(context.Response.ContentType).IsEqualTo("text/plain; charset=utf-8");
		await Assert.That(context.Response.Headers.ContentDisposition.ToString()).StartsWith("attachment; filename=THIRD-PARTY-NOTICES.txt");
		await Assert.That(Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray())).IsEqualTo("Cronos 0.11.1 MIT");
	}

	[Test]
	public async Task Serve_InALocalBuildWithoutTheFile_IsNotFound()
	{
		var context = Request();

		await NoticesEndpoint.Serve(_directory).ExecuteAsync(context);

		await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	private static DefaultHttpContext Request()
	{
		return new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(), Response = { Body = new MemoryStream() } };
	}
}
