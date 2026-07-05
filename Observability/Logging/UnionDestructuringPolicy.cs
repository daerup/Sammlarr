using System.Runtime.CompilerServices;
using Contracts;
using Serilog.Core;
using Serilog.Events;

namespace Observability.Logging;

internal sealed class UnionDestructuringPolicy : IDestructuringPolicy
{
	public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
	{
		if (value is None)
		{
			result = new ScalarValue("None");
			return true;
		}

		if ((value is IUnion || IsSome(value.GetType())) && value.GetType().GetProperty(nameof(Some<>.Value)) is { } caseProperty)
		{
			result = propertyValueFactory.CreatePropertyValue(caseProperty.GetValue(value), destructureObjects: true);
			return true;
		}

		result = null;
		return false;
	}

	private static bool IsSome(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Some<>);
}
