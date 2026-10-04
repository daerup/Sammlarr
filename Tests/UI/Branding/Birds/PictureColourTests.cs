using TestInfrastructure;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class PictureColourTests
{
	private const int Side = 48;

	private static readonly Rgb Olive = new(139, 129, 101);

	private static readonly Rgb Charcoal = new(60, 60, 60);

	private static readonly Rgb Teal = new(0x1f, 0x8a, 0x8a);

	private static readonly Rgb Orange = new(0xe8, 0x89, 0x2e);

	private static readonly Rgb Red = new(0xc4, 0x30, 0x2b);

	[Test]
	public async Task Of_APictureMostlyOneOlive_GivesThatOliveWithItsOwnLowChroma()
	{
		var picture = Draw((_, y) => y < 44 ? Olive : Charcoal);

		await Assert.That(Rounded(picture)).IsSome().And.IsEqualTo((91d, 0.04));
	}

	[Test]
	public async Task Of_AGreyPhoto_GivesNothing()
	{
		await Assert.That(PictureColour.Of(Draw(Grey))).IsNone();
	}

	[Test]
	public async Task Of_AGreyPhotoWithASmallRedScarf_GivesTheScarfsRed()
	{
		var picture = Draw((x, y) => Math.Pow((x + 0.5 - 24) / 14, 2) + Math.Pow((y + 0.5 - 40) / 6, 2) <= 1 ? Red : Grey(x, y));

		await Assert.That(Rounded(picture)).IsSome().And.IsEqualTo((27d, 0.19));
	}

	[Test]
	public async Task Of_TwoColoursSixtyFortyTealAndOrange_GivesTheTeal()
	{
		var picture = Draw((x, _) => x < 29 ? Teal : Orange);

		await Assert.That(Rounded(picture)).IsSome().And.IsEqualTo((195d, 0.09));
	}

	[Test]
	public async Task Of_APictureWhoseEveryPixelIsSeeThrough_GivesNothing()
	{
		await Assert.That(PictureColour.Of(new byte[Side * Side * 4])).IsNone();
	}

	private static Optional<(double Hue, double Chroma)> Rounded(byte[] picture)
	{
		return PictureColour.Of(picture).Map(tone => (Math.Round(tone.Hue), Math.Round(tone.Chroma, 2)));
	}

	private static Rgb Grey(int x, int y)
	{
		var reach = Math.Min(1, Math.Sqrt(Math.Pow(x + 0.5 - 24, 2) + Math.Pow(y + 0.5 - 18, 2)) / 34);
		var level = (byte)Math.Round(0xd8 - (0xd8 - 0x4a) * reach);
		return new Rgb(level, level, level);
	}

	private static byte[] Draw(Func<int, int, Rgb> pixel)
	{
		var rgba = new byte[Side * Side * 4];
		for (var y = 0; y < Side; y++)
		{
			for (var x = 0; x < Side; x++)
			{
				var colour = pixel(x, y);
				var at = (y * Side + x) * 4;
				(rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (colour.R, colour.G, colour.B, 255);
			}
		}

		return rgba;
	}

	private sealed record Rgb(byte R, byte G, byte B);
}
