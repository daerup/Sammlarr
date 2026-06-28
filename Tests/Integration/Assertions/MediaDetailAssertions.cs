using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Integration.Tests.Infrastructure.Media.Local;
using Reqnroll.Assist;

namespace Integration.Tests.Assertions;

internal static class MediaDetailAssertions
{
	public static async Task AssertItemsAsync(IReadOnlyList<MediaMatch> items, DataTable table)
	{
		var expected = table.CreateSet<ItemRow>().ToList();
		await Assert.That(items.Count).IsEqualTo(expected.Count);
		foreach (var (row, item) in expected.Zip(items))
		{
			if (!string.IsNullOrEmpty(row.Title))
				await Assert.That(item.Title).IsEqualTo(row.Title);
			if (row.Type is { } type)
				await Assert.That(item.MediaType).IsEqualTo(type);
			if (row.Poster?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
				await Assert.That(item.PosterUrl).IsNotEmpty();
			if (row.Rating is { } rating)
				await Assert.That(item.Ratings.Any(r => r.Source == rating)).IsTrue();
		}
	}

	public static async Task AssertIdsIncludeAsync(IReadOnlySet<MediaId> ids, DataTable table, LocalMediaContext cachedMedia)
	{
		foreach (var row in table.CreateSet<TitleRow>())
			await Assert.That(ids).Contains(cachedMedia.MediaIds[row.Title!]);
	}

	private sealed class ItemRow
	{
		public string? Title { get; set; }
		public MediaType? Type { get; set; }
		public string? Poster { get; set; }
		public RatingSource? Rating { get; set; }
	}

	private sealed class TitleRow
	{
		public string? Title { get; set; }
	}
}
