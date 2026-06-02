using API.Tmdb;
using Contracts.Media;
using Contracts.Media.Trending;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Tmdb;
using Reqnroll.Assist;

namespace Integration.Tests.Browse;

[Binding]
public class TmdbTrendingSteps(TmdbTrendingScenarioContext ctx, SammlarrScenarioContext sammlarr, MediaCatalogContext catalog, TmdbContext tmdb)
{
	[Given("TMDB trending page {int} includes:")]
	public void GivenTrendingPageIncludes(int page, DataTable table)
	{
		var items = table.CreateSet<TrendingInputRow>().Select(Versioned).ToList();
		sammlarr.HttpHandler.RespondWithJsonForUrl($"{TmdbEndpoints.PageParam}={page}", TmdbStub.TrendingJson(items, page));
	}

	private ResolvedVersion Versioned(TrendingInputRow row)
	{
		var title = row.Title!;
		if (catalog.Contains(title))
			return tmdb.Versions.GetValueOrDefault(title) ?? MediaVersionResolver.Resolve(catalog[title], new MediaRow());
		var type = StepTransformers.ParseMediaKind(row.Type ?? "movie");
		return new ResolvedVersion(new Dictionary<MediaProvider, string>(), type, MediaIdDefaults.DefaultYear, title, null, null, null);
	}

	[When("I request trending page {int}")]
	public async Task WhenRequestTrendingPage(int page) =>
		ctx.TrendingPage = await sammlarr.ResolveKeyed<IMediaRecommendationService>(MediaRecommendationProvider.Tmdb).GetTrendingAsync(page: page);

	[Then("the trending results are:")]
	public async Task ThenTrendingResultsAre(DataTable table)
	{
		var expected = table.CreateSet<TrendingRow>().ToList();
		var items = ctx.TrendingPage!.Items;
		await Assert.That(items.Count).IsEqualTo(expected.Count);
		foreach (var (row, item) in expected.Zip(items))
		{
			if (!string.IsNullOrEmpty(row.Title))
				await Assert.That(item.Title).IsEqualTo(row.Title);
			switch (row.Kind?.Trim().ToLowerInvariant())
			{
				case "resolved":
					await Assert.That(item).IsTypeOf<TrendingMedia>();
					break;
				case "unresolved":
					await Assert.That(item).IsTypeOf<UnresolvedTrendingMedia>();
					break;
				case null or "":
					break;
				default:
					throw new InvalidOperationException($"Unknown trending kind '{row.Kind}'. Use 'resolved' or 'unresolved'.");
			}
		}
	}

	private sealed class TrendingInputRow
	{
		public string? Title { get; set; }
		public string? Type { get; set; }
	}

	private sealed class TrendingRow
	{
		public string? Title { get; set; }
		public string? Kind { get; set; }
	}
}
