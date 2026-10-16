using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

public class RecordedStubsTests
{
	[Test]
	public async Task Write_WhenTheProviderAnswered_RecordsTheResponse()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(200, """{"id":1}"""), directory, _ => false);

		await Assert.That(recorded).Count().IsEqualTo(1);
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
	public async Task Write_WhenOnlyOneQueryVariantWasCaptured_StillDisambiguatesTheSlug()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(200, """{"id":1}""", "/tmdb/movie/1?page=2"), directory, _ => false);

		await Assert.That(recorded.Single().Slug).IsEqualTo("movie-1-2");
	}

	[Test]
	public async Task Merge_WhenEveryStackRecorded_KeepsAllOfTheirMappings()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;
		var merged = RecordedStubs.Merge([Snapshot(200, """{"id":1}"""), Snapshot(200, """{"id":2}""", "/tmdb/movie/2")]);

		var recorded = RecordedStubs.Write(merged, directory, _ => false);

		await Assert.That(recorded.Select(entry => entry.Slug)).IsEquivalentTo(["movie-1", "movie-2"]);
	}

	[Test]
	public async Task Write_ForAnAppTheStackRuns_KeepsItsPrefixSoRadarrAndSonarrDoNotCollide()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;
		var merged = RecordedStubs.Merge([
			Snapshot(200, """{"appName":"Radarr"}""", "/radarr/api/v3/system/status"),
			Snapshot(200, """{"appName":"Sonarr"}""", "/sonarr/api/v3/system/status"),
		]);

		var recorded = RecordedStubs.Write(merged, directory, _ => false);

		await Assert.That(recorded.Select(entry => entry.Path)).IsEquivalentTo(["/radarr/api/v3/system/status", "/sonarr/api/v3/system/status"]);
	}

	[Test]
	public async Task Write_ForAMetadataHostRadarrCalls_MatchesTheHostAndDropsThePrefix()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(200, """{"TmdbId":687163}""", "/radarr-metadata/v1/movie/687163"), directory, _ => false);

		var request = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "radarr-metadata", "v1-movie-687163.json")))!["request"]!;
		await Assert.That(recorded.Single().Path).IsEqualTo("/v1/movie/687163");
		await Assert.That(request["host"]!["equalTo"]!.GetValue<string>()).IsEqualTo("api.radarr.video");
		await Assert.That(request["urlPath"]!.GetValue<string>()).IsEqualTo("/v1/movie/687163");
	}

	[Test]
	public async Task Write_ForAQueryWithSpacesSentAsPlus_MatchesTheSpacesWireMockSees()
	{
		var directory = Directory.CreateTempSubdirectory("recorded-stubs-").FullName;

		var recorded = RecordedStubs.Write(Snapshot(200, """{"results":[]}""", "/tmdb/search/multi?query=Project+Hail+Mary&api_key=secret"), directory, _ => false);

		var mapping = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "tmdb", $"{recorded.Single().Slug}.json")))!;
		await Assert.That(mapping["request"]!["queryParameters"]!["query"]!["equalTo"]!.GetValue<string>()).IsEqualTo("Project Hail Mary");
		await Assert.That(mapping["request"]!["queryParameters"]!["api_key"]).IsNull();
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
