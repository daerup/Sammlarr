using System.Text.Json;

namespace E2E.Tests.Recording;

public class RecordedStubsTests
{
	[Test]
	public async Task Write_WhenTheProviderAnswered_RecordsTheResponse()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(200, """{"id":1}"""), directory, _ => false);

		await Assert.That(recorded).HasCount(1);
		await Assert.That(File.Exists(Path.Combine(directory, "tmdb", "movie-1.json"))).IsTrue();
	}

	[Test]
	[Arguments(429)]
	[Arguments(500)]
	[Arguments(401)]
	public async Task Write_WhenTheProviderRejectedTheRequest_RecordsNothing(int status)
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(status, """{"status_message":"rate limited"}"""), directory, _ => false);

		await Assert.That(recorded).IsEmpty();
		await Assert.That(Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)).IsEmpty();
	}

	[Test]
	public async Task Merge_WhenEveryStackRecorded_KeepsAllOfTheirMappings()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;
		var merged = RecordedStubs.Merge([Snapshot(200, """{"id":1}"""), Snapshot(200, """{"id":2}""", "/tmdb/movie/2")]);

		var recorded = RecordedStubs.Write(merged, directory, _ => false);

		await Assert.That(recorded.Select(entry => entry.Slug)).IsEquivalentTo(["movie-1", "movie-2"]);
	}

	private static string Snapshot(int status, string body, string url = "/tmdb/movie/1?api_key=secret") =>
		$$"""
			{
			  "mappings": [
			    {
			      "request": { "method": "GET", "url": "{{url}}" },
			      "response": { "status": {{status}}, "headers": { "Content-Type": "application/json" }, "body": {{JsonSerializer.Serialize(body)}} }
			    }
			  ]
			}
			""";
}
