using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Infrastructure;

[Binding]
public class MediaDetailSteps(MediaDetailContext ctx, CachedMediaContext cachedMedia)
{
	[Then("the item count is {int}")]
	public async Task ThenItemCount(int count) => await Assert.That(ctx.Items!).Count().IsEqualTo(count);

	[Then("the first item title is {string}")]
	public async Task ThenFirstItemTitle(string title) => await Assert.That(ctx.Items![0].Title).IsEqualTo(title);

	[Then("the first item has a poster")]
	public async Task ThenFirstItemHasPoster() => await Assert.That(ctx.Items![0].PosterUrl).IsNotEmpty();

	[Then("the first item has a TMDB rating")]
	public async Task ThenFirstItemHasTmdbRating()
	{
		await Assert.That(ctx.Items![0].Ratings).Count().IsGreaterThan(0);
		await Assert.That(ctx.Items![0].Ratings[0].Source).IsEqualTo(RatingSource.Tmdb);
	}

	[Then("the IDs include {string}")]
	public async Task ThenIdsInclude(string key) => await Assert.That(ctx.Ids!).Contains(cachedMedia.MediaIds[key]);
}
