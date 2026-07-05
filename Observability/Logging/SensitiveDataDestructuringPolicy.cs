using System.Collections.Concurrent;
using System.Reflection;
using Domain;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Logging;

internal sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
	private const string Mask = "***";

	private static readonly ConcurrentDictionary<Type, PropertyReader[]?> Plans = new();

	public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
	{
		var readers = Plans.GetOrAdd(value.GetType(), BuildPlan);
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

	private static PropertyReader[]? BuildPlan(Type type)
	{
		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
			.ToArray();

		if (!properties.Any(property => property.IsDefined(typeof(SensitiveAttribute), inherit: true)))
		{
			return null;
		}

		return properties
			.Select(property => new PropertyReader(property.Name, property.GetValue, property.IsDefined(typeof(SensitiveAttribute), inherit: true)))
			.ToArray();
	}

	private sealed record PropertyReader(string Name, Func<object?, object?> Read, bool IsSensitive);
}
