namespace Domain.Identity;

public static class PersonHues
{
	public const double First = 40;

	private const double GoldenAngle = 137.5;

	public static double After(Optional<double> last) => last.Match(hue => (hue + GoldenAngle) % 360, () => First);
}
