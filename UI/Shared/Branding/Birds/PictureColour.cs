using Contracts.Identity;

namespace UI.Shared.Branding.Birds;

internal static class PictureColour
{
	private const double Colourful = 0.025;

	private const double MostChroma = 0.2;

	private const int Clusters = 5;

	private const int Rounds = 12;

	private const byte Opaque = 128;

	internal static Optional<PictureTone> Of(ReadOnlySpan<byte> rgba)
	{
		var points = Points(rgba);
		if (points.Length == 0)
		{
			return new None();
		}

		var (centres, owners) = Cluster(points);
		return Enumerable
			.Range(0, centres.Length)
			.Select(index => (Centre: centres[index], Members: owners.Count(owner => owner == index)))
			.Where(cluster => cluster.Members > 0)
			.OrderByDescending(cluster => cluster.Members)
			.Select(cluster => cluster.Centre)
			.FirstOrNone(centre => centre.Chroma >= Colourful)
			.Map(centre => new PictureTone(centre.Hue, Math.Min(MostChroma, centre.Chroma)));
	}

	private static Lab[] Points(ReadOnlySpan<byte> rgba)
	{
		List<Lab> points = [];
		for (var at = 0; at + 3 < rgba.Length; at += 4)
		{
			if (rgba[at + 3] >= Opaque)
			{
				points.Add(Lab.Of(rgba[at], rgba[at + 1], rgba[at + 2]));
			}
		}

		return [.. points];
	}

	private static (Lab[] Centres, int[] Owners) Cluster(Lab[] points)
	{
		var centres = Seeds(points);
		int[] owners = [];
		for (var round = 0; round < Rounds; round++)
		{
			var nearest = points.Select(point => Nearest(centres, point)).ToArray();
			centres = [.. centres.Select((centre, index) => MeanOf([.. points.Where((_, at) => nearest[at] == index)]).GetValueOr(centre))];
			owners = nearest;
		}

		return (centres, owners);
	}

	private static Lab[] Seeds(Lab[] points)
	{
		List<Lab> centres = [Lab.Mean(points)];
		while (centres.Count < Clusters)
		{
			centres.Add(points.MaxBy(point => centres.Min(centre => centre.DistanceTo(point))));
		}

		return [.. centres];
	}

	private static int Nearest(Lab[] centres, Lab point) => Enumerable.Range(0, centres.Length).MinBy(index => centres[index].DistanceTo(point));

	private static Optional<Lab> MeanOf(Lab[] members) => Optional.When(members.Length > 0, () => Lab.Mean(members));

	private readonly record struct Lab(double L, double A, double B)
	{
		public double Chroma => Math.Sqrt(A * A + B * B);

		public double Hue => (Math.Atan2(B, A) * 180 / Math.PI + 360) % 360;

		public double DistanceTo(Lab other) => (L - other.L) * (L - other.L) + (A - other.A) * (A - other.A) + (B - other.B) * (B - other.B);

		public static Lab Mean(Lab[] points) => new(points.Average(point => point.L), points.Average(point => point.A), points.Average(point => point.B));

		public static Lab Of(byte red, byte green, byte blue)
		{
			var (r, g, b) = (Linear(red), Linear(green), Linear(blue));
			var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
			var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
			var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
			return new Lab(
				0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
				1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
				0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s
			);
		}

		private static double Linear(byte channel)
		{
			var value = channel / 255d;
			return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
		}
	}
}
