using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Contracts;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Logging;

internal sealed class SensitiveDataDestructuringPolicy(IReadOnlyDictionary<Type, IReadOnlySet<string>> registry) : IDestructuringPolicy
{
	private const string Mask = "***";

	private readonly ConcurrentDictionary<Type, PropertyReader[]?> _plans = new();

	public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [NotNullWhen(true)] out LogEventPropertyValue? result)
	{
		var readers = _plans.GetOrAdd(value.GetType(), BuildPlan);
		if (readers is null)
		{
			result = null;
			return false;
		}

		var properties = new List<LogEventProperty>(readers.Length);
		foreach (var reader in readers)
		{
			var propertyValue = reader.IsSensitive ? new ScalarValue(Mask) : propertyValueFactory.CreatePropertyValue(reader.Read(value), destructureObjects: true);
			properties.Add(new LogEventProperty(reader.Name, propertyValue));
		}

		result = new StructureValue(properties, value.GetType().Name);
		return true;
	}

	private PropertyReader[]? BuildPlan(Type type)
	{
		var registered = registry.GetValueOrDefault(type);

		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
			.ToArray();

		bool IsSensitive(PropertyInfo property) => property.IsDefined(typeof(SensitiveAttribute), inherit: true) || (registered?.Contains(property.Name) ?? false);

		if (!properties.Any(IsSensitive))
		{
			return null;
		}

		return [.. properties.Select(property => new PropertyReader(property.Name, property.GetValue, IsSensitive(property)))];
	}

	private sealed record PropertyReader(string Name, Func<object?, object?> Read, bool IsSensitive);
}
