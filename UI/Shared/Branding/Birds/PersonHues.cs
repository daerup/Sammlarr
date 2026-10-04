namespace UI.Shared.Branding.Birds;

internal static class PersonHues
{
	internal const double First = 40;

	private const double GoldenAngle = 137.5;

	internal static double After(Optional<double> last) => last.Match(hue => (hue + GoldenAngle) % 360, () => First);
}
