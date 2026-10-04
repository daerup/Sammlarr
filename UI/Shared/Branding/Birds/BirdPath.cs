using System.Globalization;
using System.Text.RegularExpressions;

namespace UI.Shared.Branding.Birds;

internal readonly record struct BirdPoint(double X, double Y);

internal static partial class BirdPath
{
	private const int CubicChords = 4000;

	internal static IReadOnlyList<BirdPoint> Sample(string path, int count)
	{
		var chords = Chords(path);
		var total = chords.Count == 0 ? 0 : chords[^1].Start + chords[^1].Length;
		var points = new List<BirdPoint>(count);
		var index = 0;
		for (var step = 0; step < count; step++)
		{
			var target = total * step / count;
			while (index < chords.Count - 1 && chords[index].Start + chords[index].Length < target)
			{
				index++;
			}

			var chord = chords[index];
			var share = chord.Length == 0 ? 0 : (target - chord.Start) / chord.Length;
			points.Add(new BirdPoint(chord.From.X + (chord.To.X - chord.From.X) * share, chord.From.Y + (chord.To.Y - chord.From.Y) * share));
		}

		return points;
	}

	private static List<Chord> Chords(string path)
	{
		var chords = new List<Chord>();
		var length = 0.0;
		foreach (var segment in Segments(path))
		{
			var steps = segment.Controls.Length == 4 ? CubicChords : 1;
			var previous = segment.Controls[0];
			for (var step = 1; step <= steps; step++)
			{
				var point = segment.Controls.Length == 4 ? Bezier(segment.Controls, (double)step / steps) : segment.Controls[^1];
				var piece = double.Hypot(point.X - previous.X, point.Y - previous.Y);
				chords.Add(new Chord(previous, point, length, piece));
				length += piece;
				previous = point;
			}
		}

		return chords;
	}

	private static List<Segment> Segments(string path)
	{
		var tokens = Tokens().Matches(path).Select(match => match.Value).ToList();
		var segments = new List<Segment>();
		var position = 0;
		var command = 'M';
		var current = new BirdPoint(0, 0);
		var start = current;

		double Next() => double.Parse(tokens[position++], CultureInfo.InvariantCulture);

		while (position < tokens.Count)
		{
			if (char.IsLetter(tokens[position][0]))
			{
				command = tokens[position++][0];
			}

			var relative = char.IsLower(command);
			var offset = relative ? current : new BirdPoint(0, 0);
			switch (char.ToLowerInvariant(command))
			{
				case 'm':
					current = start = new BirdPoint(offset.X + Next(), offset.Y + Next());
					command = relative ? 'l' : 'L';
					break;
				case 'l':
					var line = new BirdPoint(offset.X + Next(), offset.Y + Next());
					segments.Add(new Segment([current, line]));
					current = line;
					break;
				case 'h':
					var across = new BirdPoint(relative ? current.X + Next() : Next(), current.Y);
					segments.Add(new Segment([current, across]));
					current = across;
					break;
				case 'v':
					var down = new BirdPoint(current.X, relative ? current.Y + Next() : Next());
					segments.Add(new Segment([current, down]));
					current = down;
					break;
				case 'c':
					BirdPoint first = new(offset.X + Next(), offset.Y + Next()),
						second = new(offset.X + Next(), offset.Y + Next()),
						end = new(offset.X + Next(), offset.Y + Next());
					segments.Add(new Segment([current, first, second, end]));
					current = end;
					break;
				case 'z':
					if (current != start)
					{
						segments.Add(new Segment([current, start]));
					}

					current = start;
					break;
				default:
					throw new FormatException($"The path command '{command}' is not supported.");
			}
		}

		return segments;
	}

	private static BirdPoint Bezier(BirdPoint[] controls, double t)
	{
		var u = 1 - t;
		return new BirdPoint(
			u * u * u * controls[0].X + 3 * u * u * t * controls[1].X + 3 * u * t * t * controls[2].X + t * t * t * controls[3].X,
			u * u * u * controls[0].Y + 3 * u * u * t * controls[1].Y + 3 * u * t * t * controls[2].Y + t * t * t * controls[3].Y
		);
	}

	[GeneratedRegex(@"[a-zA-Z]|-?\d*\.?\d+(?:e[-+]?\d+)?")]
	private static partial Regex Tokens();

	private sealed record Segment(BirdPoint[] Controls);

	private readonly record struct Chord(BirdPoint From, BirdPoint To, double Start, double Length);
}
