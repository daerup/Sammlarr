using Domain.Media;

namespace Domain.Tests.Media;

public class MediaFieldTests
{
	[Test]
	public async Task Overlaps_WhenOneFieldIsShared_IsTrue() =>
		await Assert.That((MediaField.Tagline | MediaField.Poster).Overlaps(MediaField.Poster | MediaField.Genres)).IsTrue();

	[Test]
	public async Task Overlaps_WhenNoFieldIsShared_IsFalse() => await Assert.That(MediaField.Tagline.Overlaps(MediaField.Poster | MediaField.Genres)).IsFalse();

	[Test]
	public async Task Overlaps_WithNone_IsFalse() => await Assert.That(MediaField.Tagline.Overlaps(MediaField.None)).IsFalse();

	[Test]
	public async Task Count_IsTheNumberOfFieldsSet() => await Assert.That((MediaField.Title | MediaField.Seasons | MediaField.Rating).Count).IsEqualTo(3);
}
