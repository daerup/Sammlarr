using Contracts.Media;

namespace Shared.Tests.Unit;

public class MediaIdTests
{
	[Test]
	public async Task From_WhenGuidIsEmpty_Throws()
	{
		await Assert.That(() => MediaId.From(Guid.Empty)).Throws<ArgumentException>();
	}

	[Test]
	public async Task From_WhenGuidIsNonEmpty_ReturnsMediaId()
	{
		var guid = Guid.NewGuid();

		var mediaId = MediaId.From(guid);

		await Assert.That(mediaId.Value).IsEqualTo(guid);
	}
}
