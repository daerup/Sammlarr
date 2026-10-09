using Domain.Providers;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class BehindTheScenesTests
{
	private static readonly DateTimeOffset Now = Local(new DateTime(2026, 10, 8, 12, 0, 0));

	[Test]
	public async Task Status_OfAFileDownloadedOnAnEarlierDay_NamesTheDay()
	{
		var downloads = new Dictionary<RemoteService, DateTimeOffset> { [RemoteService.AnimeLists] = Local(new DateTime(2026, 10, 7, 22, 26, 0)) };

		var status = BehindTheScenes.Status(RemoteService.AnimeLists, downloads, Now);

		await Assert.That(status).IsEqualTo(new BehindTheScenesStatus("Downloaded Oct 7, 22:26", true));
	}

	[Test]
	public async Task Status_OfAServiceWithoutAFile_IsReady()
	{
		var status = BehindTheScenes.Status(RemoteService.Wikidata, new Dictionary<RemoteService, DateTimeOffset>(), Now);

		await Assert.That(status).IsEqualTo(new BehindTheScenesStatus("Ready", true));
	}

	private static DateTimeOffset Local(DateTime at) => new(at, TimeZoneInfo.Local.GetUtcOffset(at));
}
