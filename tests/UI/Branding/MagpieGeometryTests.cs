using UI.Shared.Branding;

namespace UI.Tests.Branding;

public class MagpieGeometryTests
{
	[Test]
	public async Task LogoRays_AreFiveShortEvenRaysAroundTheStar()
	{
		await Assert.That(MagpieGeometry.LogoRays.Count(command => command == 'M')).IsEqualTo(5);
		await Assert.That(MagpieGeometry.LogoRays).StartsWith("M247.92 49.15L258.81 50.72");
	}

	[Test]
	public async Task RatingRays_AreTheSameRaysCloserToTheStar()
	{
		await Assert.That(MagpieGeometry.RatingRays.Count(command => command == 'M')).IsEqualTo(5);
		await Assert.That(MagpieGeometry.RatingRays).StartsWith("M245.45 48.79L251.39 49.65");
	}
}
