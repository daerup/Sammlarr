using API.Tmdb;
using Contracts.Media;
using Contracts.Media.Trending;
using Domain.Media;
using Domain.Providers;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Tmdb;
using Reqnroll;
using Reqnroll.Assist;
using Reqnroll.Assist.Attributes;

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

	[Given("TMDB trending page {int} is empty")]
	public void GivenTrendingPageEmpty(int page) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl($"{TmdbEndpoints.PageParam}={page}", TmdbStub.TrendingJson([], page));

	private ResolvedVersion Versioned(TrendingInputRow row)
	{
		var key = row.Key!;
		if (catalog.Contains(key))
			return tmdb.Versions.GetValueOrDefault(key) ?? MediaVersionResolver.Build(catalog[key]);
		var type = StepTransformers.ParseMediaKind(row.Type ?? "movie");
		return new ResolvedVersion(new Dictionary<MediaProvider, string>(), type, MediaIdDefaults.DefaultYear, key, null, null, null);
	}

	[When("I request trending page {int}")]
	public async Task WhenRequestTrendingPage(int page) =>
		ctx.TrendingPage = await sammlarr.ResolveKeyed<ITrendingService>(TrendingProvider.Tmdb).GetTrendingAsync(page: page);

	[Then("the trending results are empty")]
	public async Task ThenTrendingResultsEmpty() => await Assert.That(ctx.TrendingPage!.Items).IsEmpty();

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
					await Assert.That(item is IdentifiedMedia).IsTrue();
					break;
				case "unresolved":
					await Assert.That(item is UnmatchedMedia).IsTrue();
					break;
				case null or "":
					break;
				default:
					throw new InvalidOperationException($"Unknown trending kind '{row.Kind}'. Use 'resolved' or 'unresolved'.");
			}

			if (row.Ids is { Count: > 0 } ids)
			{
				foreach (var token in ids)
				{
					var (provider, value) = MediaProviders.ParseToken(token);
					await Assert.That(item.Externals.Any(external => external.Source == provider && external.Value == value)).IsTrue();
				}
			}
		}
	}

	private sealed class TrendingInputRow
	{
		[TableAliases("^#$")]
		public string? Key { get; set; }
		public string? Type { get; set; }
	}

	private sealed class TrendingRow
	{
		public string? Title { get; set; }
		public string? Kind { get; set; }
		public IReadOnlyList<string>? Ids { get; set; }
	}
}
