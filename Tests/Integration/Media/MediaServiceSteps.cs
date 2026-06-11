using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Integration.Tests.Infrastructure.Common;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
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
		ctx.Detail = results[0] is IMediaDetail detail ? detail : null;
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
		ctx.Identifications = [.. results.Select(r => r is Identification identification ? identification : null)];
	}

	[When("I try to resolve:")]
	public async Task WhenTryResolveMany(DataTable table) => await exceptionContext.TryExecuteAsync(() => WhenResolveMany(table));

	private async Task ResolveSingle(string id, string? title, MediaProvider target)
	{
		var request = BuildRequest([ParseId(id)], title, target);
		var results = await Service.IdentifyAsync([request]).ToListAsync();
		ctx.Identification = results[0] is Identification identification ? identification : null;
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

	[Then("the detail title is {string}")]
	public async Task ThenDetailTitle(string title) => await Assert.That(ctx.Detail!.Title).IsEqualTo(title);

	[Then("the detail is:")]
	public async Task ThenDetailIs(DataTable table)
	{
		var row = table.CreateSet<MediaRow>().Single();
		var detail = ctx.Detail!;
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
				await Assert.That(detail.Ratings.Any(r => r.Source == Enum.Parse<RatingSource>(rating, ignoreCase: true))).IsTrue();
		}
	}

	[Then("the detail is cached as {string} fetched at {string}")]
	public async Task ThenDetailCachedAs(string key, string fetched)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var mediaId = local.MediaIds[key].Value;
		var metadata = await sammlarr.DbContext.MediaMetadata.AsNoTracking().Include(m => m.Ratings).FirstAsync(m => m.MediaId == mediaId);
		var detail = ctx.Detail!;

		await Assert.That(metadata.Title).IsEqualTo(detail.Title);
		await Assert.That(metadata.Summary).IsEqualTo(detail.Summary);
		await Assert.That(metadata.Genres).IsEquivalentTo(detail.Genres);
		await Assert.That(metadata.Ratings.Select(r => r.Source)).IsEquivalentTo(detail.Ratings.Select(r => r.Source));
		await Assert.That(metadata.LastFetchedAt).IsEqualTo(Cells.Date(fetched)!.Value);
	}

	[Then("no id is resolved")]
	public async Task ThenNoIdResolved() => await Assert.That(ctx.Identification).IsNull();

	[Then("the matching id is {string}")]
	public async Task ThenMatchingId(string id)
	{
		await Assert.That(ctx.Identification).IsNotNull();
		var expected = ParseId(id);
		await Assert.That(ctx.Identification!.TargetExternal.Source).IsEqualTo(expected.Source);
		await Assert.That(ctx.Identification!.TargetExternal.Value).IsEqualTo(expected.Value);
	}

	[Then("they resolve to one media")]
	public async Task ThenResolveToOneMedia()
	{
		await Assert.That(ctx.Identifications!).IsNotEmpty();
		await Assert.That(ctx.Identifications!.Select(identification => identification!.Id).Distinct().Count()).IsEqualTo(1);
	}

	[Then("the id {string} is stored")]
	public async Task ThenIdStored(string id)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var external = ParseId(id);
		var exists = await sammlarr.DbContext.ExternalMediaIds.AnyAsync(e => e.Source == external.Source && e.Id == external.Value);
		await Assert.That(exists).IsTrue();
	}

	[Then("the media count is {int}")]
	public async Task ThenMediaCount(int count)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		await Assert.That(await sammlarr.DbContext.Media.CountAsync()).IsEqualTo(count);
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

	[Then("media is cached as:")]
	public async Task ThenCachedAs(DataTable table)
	{
		foreach (var row in table.CreateSet<MediaRow>())
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
				await Assert.That(metadata.Ratings.Any(r => r.Source == Enum.Parse<RatingSource>(ratingSource, ignoreCase: true))).IsTrue();
			if (Cells.Date(row.Fetched) is { } fetched)
				await Assert.That(metadata.LastFetchedAt).IsEqualTo(fetched);
		}
	}
}
