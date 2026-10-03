using System.Globalization;

namespace UI.Shared.Branding;

internal static class MagpieGeometry
{
	public const string Star =
		"M226.79,34.99l11.62,-2.17c0.15,-0.03 0.3,-0.05 0.46,-0.05c1.26,0 2.28,1.02 2.28,2.28c0,0.49 -0.15,0.94 -0.41,1.31l-6.67,9.78l5.65,10.39c0.19,0.33 0.3,0.72 0.3,1.13c0,1.26 -1.02,2.28 -2.28,2.28c-0.24,0 -0.47,-0.04 -0.69,-0.11l-11.33,-3.31l-8.16,8.61c-0.42,0.44 -1,0.71 -1.66,0.71c-1.24,0 -2.25,-0.99 -2.28,-2.22l-0.35,-11.85l-10.67,-5.08c-0.79,-0.36 -1.34,-1.15 -1.34,-2.08c0,-1.01 0.65,-1.86 1.56,-2.16l11.12,-3.98l1.54,-11.73c0.13,-1.13 1.09,-2.01 2.26,-2.01c0.74,0 1.4,0.35 1.82,0.9z";

	public const string StarViewBox = "197.2 24.28 48 48";

	public const double StarLeft = 201.26;

	public const double StarTop = 24.73;

	public const double StarWidth = 39.89;

	public const double StarHeight = 41.11;

	private const double CenterX = 221.2;

	private const double CenterY = 45.3;

	private static readonly double[] RayAngles = [8.2, 80.2, 152.2, 224.2, 296.2];

	public static readonly string LogoRays = ShortRays(27, 38);

	public static readonly string RatingRays = ShortRays(24.5, 30.5);

	private static string ShortRays(double inner, double outer)
	{
		return string.Concat(RayAngles.Select(angle => Ray(angle * Math.PI / 180, inner, outer)));
	}

	private static string Ray(double radians, double inner, double outer)
	{
		return string.Create(
			CultureInfo.InvariantCulture,
			$"M{CenterX + inner * Math.Cos(radians):0.00} {CenterY + inner * Math.Sin(radians):0.00}L{CenterX + outer * Math.Cos(radians):0.00} {CenterY + outer * Math.Sin(radians):0.00}"
		);
	}
}
