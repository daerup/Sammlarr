using System.Runtime.CompilerServices;
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

		if ((value is IUnion || IsWrapper(value.GetType())) && value.GetType().GetProperty(nameof(Some<>.Value)) is { } caseProperty)
		{
			result = propertyValueFactory.CreatePropertyValue(caseProperty.GetValue(value), destructureObjects: true);
			return true;
		}

		result = null;
		return false;
	}

	private static bool IsWrapper(Type type) =>
		type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Some<>) || type.GetGenericTypeDefinition() == typeof(Success<>));
}
