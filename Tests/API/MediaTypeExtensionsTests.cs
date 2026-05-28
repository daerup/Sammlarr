using Contracts.Media;

namespace API.Tests;

public class MediaTypeExtensionsTests
{
	[Test]
	[Arguments("movie", MediaType.Movie)]
	[Arguments("movies", MediaType.Movie)]
	[Arguments("show", MediaType.Show)]
	[Arguments("series", MediaType.Show)]
	[Arguments("tv", MediaType.Show)]
	public async Task TryParse_WhenKnownValue_ReturnsTrueAndExpectedType(string value, MediaType expected)
	{
		var success = MediaType.TryParse(value, out var parsed);
		await Assert.That(success).IsTrue();
		await Assert.That(parsed).IsEqualTo(expected);
	}

	[Test]
	public async Task TryParse_WhenUnknownValue_ReturnsFalse()
	{
		var success = MediaType.TryParse("documentary", out var parsed);

		await Assert.That(success).IsFalse();
		await Assert.That(parsed).IsEqualTo(MediaType.Unknown);
	}

	[Test]
	[Arguments("movie", MediaType.Movie)]
	[Arguments("movies", MediaType.Movie)]
	[Arguments("show", MediaType.Show)]
	[Arguments("series", MediaType.Show)]
	[Arguments("tv", MediaType.Show)]
	public async Task Parse_WhenKnownValue_ReturnsExpectedType(string value, MediaType expected)
	{
		var parsed = MediaType.Parse(value);
		await Assert.That(parsed).IsEqualTo(expected);
	}
}
