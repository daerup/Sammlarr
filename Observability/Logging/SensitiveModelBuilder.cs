using System.Linq.Expressions;

namespace Observability.Logging;

internal sealed class SensitiveModelBuilder
{
	private readonly Dictionary<Type, HashSet<string>> _properties = [];

	public SensitiveModelBuilder Sensitive<T>(Expression<Func<T, object?>> property)
	{
		var member = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } boxed ? boxed.Operand : property.Body;
		var name = ((MemberExpression)member).Member.Name;
		if (!_properties.TryGetValue(typeof(T), out var names))
		{
			names = new HashSet<string>(StringComparer.Ordinal);
			_properties[typeof(T)] = names;
		}
		names.Add(name);
		return this;
	}

	public IReadOnlyDictionary<Type, IReadOnlySet<string>> Build() => _properties.ToDictionary(entry => entry.Key, entry => (IReadOnlySet<string>)entry.Value);
}
