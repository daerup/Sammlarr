using Domain.Blocklist;
using Domain.Media;

namespace Domain.Tests.Blocklist;

public class BlocklistItemTests
{
	[Test]
	public async Task With_MapsTheItemOntoItsDetails_KeepingWhenItWasBlocklisted()
	{
		var saw = MediaId.From(new Guid(1, 0, 0, new byte[8]));
		IdentifiedMedia media = new PartialMovie { Identity = new MediaIdentity(saw, []), Title = "Saw" };

		var blocklisted = new BlocklistItem(saw, DateTimeOffset.UnixEpoch).With(media);

		await Assert.That(blocklisted).IsEqualTo(new BlocklistedMedia(media, DateTimeOffset.UnixEpoch));
	}
}
