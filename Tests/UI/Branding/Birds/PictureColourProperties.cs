using CsCheck;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class PictureColourProperties
{
	private static readonly Gen<byte[]> Pictures = Gen.Byte.Array[0, 64 * 4];

	[Test]
	public void Of_AnyPicture_GivesNothingOrAHueOnTheWheelAtAColourfulChroma()
	{
		Pictures.Sample(rgba => PictureColour.Of(rgba).Match(tone => tone.Hue is >= 0 and < 360 && tone.Chroma is >= 0.025 and <= 0.2, () => true));
	}

	[Test]
	public void Of_TheSamePicture_AlwaysGivesTheSameColour()
	{
		Pictures.Sample(rgba => Said(rgba) == Said(rgba));
	}

	private static string Said(byte[] rgba)
	{
		return PictureColour.Of(rgba).Match(tone => $"{tone.Hue}/{tone.Chroma}", () => "none");
	}
}
