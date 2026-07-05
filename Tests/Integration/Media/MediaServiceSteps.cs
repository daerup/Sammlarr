using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Common;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Media;

[Binding]
public class MediaServiceSteps(MediaServiceContext ctx, LocalMediaContext local, SammlarrScenarioContext sammlarr, ExceptionContext exceptionContext)
{
	private IMediaService Service => sammlarr.Resolve<IMediaService>();

	[When("I get details for {string}")]
	public async Task WhenGetDetails(string key)
	{
		var results = await Service.GetDetailsAsync([local.MediaIds[key]]).ToListAsync();
		ctx.Detail = results[0] is Success<IdentifiedMedia>(var detail) ? detail.ToMatch() : (MediaLookup?)null;
	}

	[When("I resolve {string} to a {MediaProvider} id")]
	public async Task WhenResolveTo(string id, MediaProvider target) => await ResolveSingle(id, title: null, target);

	[When("I resolve {string} titled {string} to a {MediaProvider} id")]
	public async Task WhenResolveTitledTo(string id, string title, MediaProvider target) => await ResolveSingle(id, title, target);

	[When("I try to resolve {string} titled {string} to a {MediaProvider} id")]
	public async Task WhenTryResolveTitledTo(string id, string title, MediaProvider target) =>
		await exceptionContext.TryExecuteAsync(() => ResolveSingle(id, title, target));

	[When("I resolve:")]
	public async Task WhenResolveMany(DataTable table)
	{
		var requests = table.CreateSet<ResolveRow>().Select(BuildRequest).ToList();
		var results = await Service.IdentifyAsync(requests).ToListAsync();
		ctx.Identifications = [.. results.Select(r => r is Success<MediaIdentity>(var identification) ? identification : (MediaIdentity?)null)];
	}

	[When("I try to resolve:")]
	public async Task WhenTryResolveMany(DataTable table) => await exceptionContext.TryExecuteAsync(() => WhenResolveMany(table));

	private async Task ResolveSingle(string id, string? title, MediaProvider target)
	{
		var request = BuildRequest([ParseId(id)], title, target);
		var results = await Service.IdentifyAsync([request]).ToListAsync();
		ctx.Identification = results[0] is Success<MediaIdentity>(var identification) ? identification : null;
		ctx.Identifications = [ctx.Identification];
	}

	private static ResolveRequest BuildRequest(ResolveRow row)
	{
		var candidates = (row.Candidates ?? throw new InvalidOperationException("Each resolve row requires a 'candidates' column."))
			.Select(pair => ExternalIdFactory.Create(pair.Key, pair.Value, MediaType.Movie))
			.ToList();
		return BuildRequest(candidates, Cells.Text(row.Title), MediaProviders.Parse(row.To!));
	}

	private static ResolveRequest BuildRequest(IReadOnlyList<IExternalMediaId> candidates, string? title, MediaProvider target)
	{
		Optional<ResolutionHints> hints = new None();
		if (title is not null)
			hints = Some.Of(
				new ResolutionHints
				{
					Title = title,
					Year = 0,
					MediaType = MediaType.Movie,
				}
			);
		return new ResolveRequest(candidates, target, hints);
	}

	[When("I invalidate {string}")]
	public async Task WhenInvalidate(string key) => await Service.InvalidateAsync([local.MediaIds[key]]);

	[When("I invalidate {string} and {string}")]
	public async Task WhenInvalidateMany(string first, string second) => await Service.InvalidateAsync([local.MediaIds[first], local.MediaIds[second]]);

	[When("I get details for:")]
	public async Task WhenGetDetailsMany(DataTable table)
	{
		var keys = table.Rows.Select(row => row["media"]).ToList();
		var results = await Service.GetDetailsAsync([.. keys.Select(key => local.MediaIds[key])]).ToListAsync();
		ctx.DetailsByKey.Clear();
		foreach (var (key, result) in keys.Zip(results))
			ctx.DetailsByKey[key] = result is Success<IdentifiedMedia>(var detail) ? detail.ToMatch() : (MediaLookup?)null;
	}

	[Then("the detail title for {string} is {string}")]
	public async Task ThenDetailTitleFor(string key, string title) => await Assert.That(ctx.DetailsByKey[key]!.Value.Title).IsEqualTo(title);

	[Then("the detail title is {string}")]
	public async Task ThenDetailTitle(string title) => await Assert.That(ctx.Detail!.Value.Title).IsEqualTo(title);

	[Then("the detail is:")]
	public async Task ThenDetailIs(DataTable table)
	{
		var row = table.CreateSet<DetailRow>().Single();
		var detail = ctx.Detail!.Value;
		if (row.Title is { } title)
			await Assert.That(detail.Title).IsEqualTo(title);
		if (row.Summary is { } summary)
			await Assert.That(detail.Summary).IsEqualTo(summary);
		if (row.Genres is { Count: > 0 } genres)
			await Assert.That(detail.Genres).IsEquivalentTo(genres);
		if (row.Rating is { } rating)
		{
			if (rating.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
				await Assert.That(detail.Ratings).IsEmpty();
			else
				await Assert.That(detail.Ratings.Any(r => r.Source == Enum.Parse<RatingProvider>(rating, ignoreCase: true))).IsTrue();
		}
	}

	[Then("the detail is cached as {string} fetched at {string}")]
	public async Task ThenDetailCachedAs(string key, string fetched)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var mediaId = local.MediaIds[key].Value;
		var metadata = await sammlarr.DbContext.MediaMetadata.AsNoTracking().Include(m => m.Ratings).FirstAsync(m => m.MediaId == mediaId);
		var detail = ctx.Detail!.Value;

		await Assert.That(metadata.Title).IsEqualTo(detail.Title);
		await Assert.That(metadata.Summary).IsEqualTo(detail.Summary);
		await Assert.That(metadata.Genres).IsEquivalentTo(detail.Genres);
		await Assert.That(metadata.Ratings.Select(r => r.Source)).IsEquivalentTo(detail.Ratings.Select(r => r.Source));
		await Assert.That(metadata.LastFetchedAt).IsEqualTo(Cells.Date(fetched)!.Value);
	}

	[Then("the resolution returns:")]
	public async Task ThenResolutionReturns(DataTable table)
	{
		var row = table.CreateSet<ResolutionRow>().Single();

		if (row.ResolvedId is { } resolvedId)
		{
			await Assert.That(ctx.Identification).IsNotNull();
			var expected = ParseId(resolvedId);
			var target = ctx.Identification!.Value.Externals.OfProvider(expected.Source);
			await Assert.That(target).IsNotNull();
			await Assert.That(target!.Value).IsEqualTo(expected.Value);
		}

		if (row.StoredIds is { Count: > 0 } storedIds)
			await AssertStoredAsync(storedIds);
	}

	[Then("the resolution is empty")]
	public async Task ThenResolutionEmpty() => await Assert.That(ctx.Identification).IsNull();

	[Then("the stored ids are {string}")]
	public async Task ThenStoredIdsAre(string ids) => await AssertStoredAsync([.. ids.Split(',').Select(token => token.Trim())]);

	private async Task AssertStoredAsync(IReadOnlyList<string> tokens)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		foreach (var token in tokens)
		{
			var external = ParseId(token);
			var exists = await sammlarr.DbContext.ExternalMediaIds.AnyAsync(e => e.Source == external.Source && e.Id == external.Value);
			await Assert.That(exists).IsTrue();
		}
	}

	private sealed class ResolutionRow
	{
		public string? ResolvedId { get; set; }
		public IReadOnlyList<string>? StoredIds { get; set; }
	}

	private static IExternalMediaId ParseId(string token)
	{
		var parts = token.Split(':', 2);
		return ExternalIdFactory.Create(MediaProviders.Parse(parts[0].Trim()), parts[1].Trim(), MediaType.Movie);
	}

	public sealed class ResolveRow
	{
		public IReadOnlyDictionary<MediaProvider, string>? Candidates { get; set; }
		public string? Title { get; set; }
		public string? To { get; set; }
	}

	private sealed class DetailRow
	{
		public string? Title { get; set; }
		public string? Summary { get; set; }
		public IReadOnlyList<string>? Genres { get; set; }
		public string? Rating { get; set; }
	}

	private sealed class CachedAssertRow
	{
		public string? Media { get; set; }
		public string? Title { get; set; }
		public string? Summary { get; set; }
		public IReadOnlyList<string>? Genres { get; set; }
		public string? Rating { get; set; }
		public string? Fetched { get; set; }
	}

	[Then("media is cached as:")]
	public async Task ThenCachedAs(DataTable table)
	{
		foreach (var row in table.CreateSet<CachedAssertRow>())
		{
			sammlarr.DbContext.ChangeTracker.Clear();
			var mediaId = local.MediaIds[row.Media!].Value;
			var metadata = await sammlarr.DbContext.MediaMetadata.AsNoTracking().Include(m => m.Ratings).FirstAsync(m => m.MediaId == mediaId);
			if (row.Title is { } title)
				await Assert.That(metadata.Title).IsEqualTo(title);
			if (row.Summary is { } summary)
				await Assert.That(metadata.Summary).IsEqualTo(summary);
			if (row.Genres is { Count: > 0 } genres)
				foreach (var genre in genres)
					await Assert.That(metadata.Genres).Contains(genre);
			if (row.Rating is { } ratingSource)
				await Assert.That(metadata.Ratings.Any(r => r.Source == Enum.Parse<RatingProvider>(ratingSource, ignoreCase: true))).IsTrue();
			if (Cells.Date(row.Fetched) is { } fetched)
				await Assert.That(metadata.LastFetchedAt).IsEqualTo(fetched);
		}
	}
}
