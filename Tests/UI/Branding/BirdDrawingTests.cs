using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsCheck;
using Domain.Identity.Birds;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding;

public partial class BirdDrawingTests
{
	private static readonly JsonElement Reference = JsonDocument
		.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Branding", "BirdReference.json")))
		.RootElement;

	public static IEnumerable<Func<(BirdPose Pose, string Name)>> Poses()
	{
		string[] names = ["perched", "looking up", "pecking", "crouching", "hopping", "flying", "downstroke", "gliding", "wings open", "singing"];
		return names.Select((name, index) => (Func<(BirdPose, string)>)(() => ((BirdPose)index, name)));
	}

	[Test]
	[MethodDataSource(nameof(Poses))]
	public async Task Draw_ForEachPoseAtRest_MatchesTheMockupsDrawingInChrome(BirdPose pose, string name)
	{
		var chrome = Reference.GetProperty("poses").GetProperty(name);

		var art = BirdDrawing.Draw(new BirdGenome(pose, 0, 0, 0, 1, false, false, BirdCrest.None, false, 2), "B", "C");

		await Assert.That(art.ViewBox).IsEqualTo(chrome.GetProperty("box").GetString());
		await Assert.That(Skeleton(art.Markup)).IsEqualTo(Skeleton(chrome.GetProperty("svg").GetString()!));
		await Assert.That(Differences(art.Markup, chrome.GetProperty("svg").GetString()!)).IsLessThanOrEqualTo(pose is BirdPose.Crouching ? 1 : 0);
	}

	public static IEnumerable<Func<string>> People() => Reference.GetProperty("seeds").EnumerateObject().Select(seed => (Func<string>)(() => seed.Name));

	[Test]
	[MethodDataSource(nameof(People))]
	public async Task Draw_ForEachOfTheMockupsPeople_MatchesTheMockupsDrawingInChrome(string seed)
	{
		var chrome = Reference.GetProperty("seeds").GetProperty(seed);

		var art = BirdDrawing.Draw(BirdGenome.Of(seed), "oklch(0.82 0.11 200)", "oklch(0.31 0.06 200)");

		await Assert.That(Close(art.ViewBox, chrome.GetProperty("box").GetString()!)).IsTrue();
		await Assert.That(Skeleton(art.Markup)).IsEqualTo(Skeleton(chrome.GetProperty("svg").GetString()!));
		await Assert.That(Close(art.Markup, chrome.GetProperty("svg").GetString()!)).IsTrue();
	}

	[Test]
	public void Draw_ForAnyGenes_DrawsAWholeBirdInsideItsFrame()
	{
		Gen.UInt.Sample(
			genes =>
			{
				var genome = BirdGenome.From(genes);
				var art = BirdDrawing.Draw(genome, "B", "C");
				var box = Numbers(art.ViewBox);
				var centre = (X: box[0] + box[2] / 2, Y: box[1] + box[3] / 2);
				return box.Count == 4 && box[2] > 0 && box[2] == box[3] && Outline(art.Markup, genome).All(point => Inside(point, centre, box[2] / 2));
			},
			iter: 200
		);
	}

	[Test]
	public async Task Draw_ForTheSameGenes_AlwaysDrawsTheSameBird()
	{
		var genome = BirdGenome.Of("guest");

		await Assert.That(BirdDrawing.Draw(genome, "B", "C")).IsEqualTo(BirdDrawing.Draw(genome, "B", "C"));
	}

	[Test]
	[Arguments(0.25, "0.3")]
	[Arguments(-0.25, "-0.3")]
	[Arguments(0.35, "0.3")]
	[Arguments(1.45, "1.4")]
	[Arguments(-0.0, "0.0")]
	[Arguments(-0.04, "-0.0")]
	public async Task Fixed_RoundsLikeJavaScriptsToFixed(double value, string expected)
	{
		await Assert.That(BirdDrawing.Fixed(value)).IsEqualTo(expected);
	}

	private static int Differences(string mine, string chrome)
	{
		var ours = Numbers(mine);
		var theirs = Numbers(chrome);
		return ours.Count != theirs.Count ? int.MaxValue : ours.Zip(theirs).Count(pair => Math.Abs(pair.First - pair.Second) > 1e-9);
	}

	private static bool Close(string mine, string chrome) => Numbers(mine).Zip(Numbers(chrome)).All(pair => Math.Abs(pair.First - pair.Second) <= 0.1 + 1e-9);

	private static IEnumerable<(double X, double Y)> Outline(string markup, BirdGenome genome)
	{
		var shape = BirdShape.Of(genome);
		var path = OutlinePath().Match(markup).Groups[1].Value;
		var numbers = Numbers(path);
		return Enumerable.Range(0, numbers.Count / 2).Select(index => Placed(numbers[2 * index], numbers[2 * index + 1], shape.Tilt, shape.Lift, shape.FacesLeft));
	}

	private static (double X, double Y) Placed(double x, double y, int tilt, int lift, bool facesLeft)
	{
		var radians = tilt * Math.PI / 180;
		var turnedX = 176 + (x - 176) * Math.Cos(radians) - (y - 198.8) * Math.Sin(radians);
		var turnedY = 198.8 + (x - 176) * Math.Sin(radians) + (y - 198.8) * Math.Cos(radians) + lift;
		return (facesLeft ? 256 - turnedX : turnedX, turnedY);
	}

	private static bool Inside((double X, double Y) point, (double X, double Y) centre, double radius) =>
		double.Hypot(point.X - centre.X, point.Y - centre.Y) <= radius;

	private static string Skeleton(string svg) => Number().Replace(svg, "#");

	private static List<double> Numbers(string text)
	{
		return [.. Number().Matches(text).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))];
	}

	[GeneratedRegex("<path d=\"M([0-9.,L-]+)Z\"")]
	private static partial Regex OutlinePath();

	[GeneratedRegex(@"-?\d*\.?\d+")]
	private static partial Regex Number();
}
