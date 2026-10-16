using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

internal static class JsonContract
{
	internal static IReadOnlyList<string> Incompatibilities(string liveJson, string committedJson)
	{
		var problems = new List<string>();
		Compare("$", Derive(JsonNode.Parse(liveJson)), Derive(JsonNode.Parse(committedJson)), problems);
		return problems;
	}

	private abstract record Shape
	{
		internal abstract string Kind { get; }
	}

	private sealed record ObjectShape(IReadOnlyDictionary<string, Shape> Fields) : Shape
	{
		internal override string Kind => "object";
	}

	private sealed record ArrayShape(Shape Element) : Shape
	{
		internal override string Kind => "array";
	}

	private sealed record ValueShape(IReadOnlySet<string> Types) : Shape
	{
		internal override string Kind => Types.Count == 0 ? "empty" : string.Join('|', Types.OrderBy(type => type, StringComparer.Ordinal));
	}

	private static Shape Derive(JsonNode? node) =>
		node switch
		{
			JsonObject obj => new ObjectShape(obj.ToDictionary(pair => pair.Key, pair => Derive(pair.Value))),
			JsonArray array => new ArrayShape(array.Select(Derive).Aggregate(Empty, Merge)),
			JsonValue value => new ValueShape(new HashSet<string> { TypeOf(value) }),
			_ => new ValueShape(new HashSet<string> { "null" }),
		};

	private static Shape Empty => new ValueShape(new HashSet<string>());

	private static Shape Merge(Shape left, Shape right) =>
		(left, right) switch
		{
			(ValueShape { Types.Count: 0 }, _) => right,
			(_, ValueShape { Types.Count: 0 }) => left,
			(ObjectShape leftObject, ObjectShape rightObject) => MergeObjects(leftObject, rightObject),
			(ArrayShape leftArray, ArrayShape rightArray) => new ArrayShape(Merge(leftArray.Element, rightArray.Element)),
			(ValueShape leftValue, ValueShape rightValue) => new ValueShape(leftValue.Types.Union(rightValue.Types).ToHashSet()),
			_ => new ValueShape(new HashSet<string> { "mixed" }),
		};

	private static Shape MergeObjects(ObjectShape left, ObjectShape right)
	{
		var fields = new Dictionary<string, Shape>();
		foreach (var key in left.Fields.Keys.Union(right.Fields.Keys))
		{
			fields[key] = left.Fields.TryGetValue(key, out var leftField)
				? right.Fields.TryGetValue(key, out var rightField)
					? Merge(leftField, rightField)
					: leftField
				: right.Fields[key];
		}

		return new ObjectShape(fields);
	}

	private static void Compare(string path, Shape live, Shape committed, List<string> problems)
	{
		if (committed is ValueShape { Types: var known } && known.Contains("null"))
		{
			return;
		}

		switch (live)
		{
			case ValueShape value:
				CompareValue(path, value, committed, problems);
				return;
			case ObjectShape liveObject when committed is ObjectShape committedObject:
				CompareObject(path, liveObject, committedObject, problems);
				return;
			case ArrayShape liveArray when committed is ArrayShape committedArray:
				Compare($"{path}[]", liveArray.Element, committedArray.Element, problems);
				return;
			case ObjectShape:
			case ArrayShape:
				problems.Add($"{path}: {live.Kind} where the fixture has {committed.Kind}");
				return;
		}
	}

	private static void CompareValue(string path, ValueShape live, Shape committed, List<string> problems)
	{
		var required = live.Types.Where(type => type != "null").ToList();
		if (required.Count == 0)
		{
			return;
		}

		if (committed is not ValueShape committedValue)
		{
			problems.Add($"{path}: value where the fixture has {committed.Kind}");
			return;
		}

		foreach (var type in required.Where(type => !committedValue.Types.Contains(type)))
		{
			problems.Add($"{path}: type '{type}' not in the fixture ({committedValue.Kind})");
		}
	}

	private static void CompareObject(string path, ObjectShape live, ObjectShape committed, List<string> problems)
	{
		foreach (var (key, value) in live.Fields)
		{
			if (committed.Fields.TryGetValue(key, out var committedField))
			{
				Compare($"{path}.{key}", value, committedField, problems);
			}
			else
			{
				problems.Add($"{path}.{key}: field not in the fixture");
			}
		}
	}

	private static string TypeOf(JsonValue value) =>
		value.GetValueKind() switch
		{
			JsonValueKind.String => "string",
			JsonValueKind.Number => "number",
			JsonValueKind.True => "boolean",
			JsonValueKind.False => "boolean",
			_ => "null",
		};
}
