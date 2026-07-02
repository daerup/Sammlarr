using Domain.Media;

namespace Domain.Tests.Media;

public class MediaIdTests
{
	[Test]
	public async Task From_WhenEmptyGuid_Throws()
	{
		await Assert.That(() => MediaId.From(Guid.Empty)).Throws<ArgumentException>();
	}

	[Test]
	public async Task From_WhenValidGuid_KeepsValue()
	{
		var guid = Guid.NewGuid();

		var id = MediaId.From(guid);

		await Assert.That(id.Value).IsEqualTo(guid);
	}

	[Test]
	public async Task Equality_WhenSameGuid_AreEqual()
	{
		var guid = Guid.NewGuid();

		await Assert.That(MediaId.From(guid)).IsEqualTo(MediaId.From(guid));
	}
}
