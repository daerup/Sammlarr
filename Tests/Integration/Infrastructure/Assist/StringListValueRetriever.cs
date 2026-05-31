using Integration.Tests.Infrastructure.Media;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Assist;

public sealed class StringListValueRetriever : IValueRetriever
{
	public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType) => propertyType == typeof(IReadOnlyList<string>);

	public object Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
	{
		var value = keyValuePair.Value;
		if (string.IsNullOrEmpty(value) || Cells.IsNull(value))
			return Array.Empty<string>();
		return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}
