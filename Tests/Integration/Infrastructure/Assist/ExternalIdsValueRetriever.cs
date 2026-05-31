using Contracts.Media;
using Integration.Tests.Infrastructure.Media;
using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Assist;

public sealed class ExternalIdsValueRetriever : IValueRetriever
{
	public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType) =>
		propertyType == typeof(IReadOnlyDictionary<MediaProvider, string>);

	public object Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
	{
		var ids = new Dictionary<MediaProvider, string>();
		foreach (var token in keyValuePair.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var parts = token.Split(':', 2);
			if (parts.Length != 2)
				throw new InvalidOperationException($"Id token '{token}' must be written as 'provider:value'.");
			ids[MediaProviders.Parse(parts[0].Trim())] = parts[1].Trim();
		}
		return ids;
	}
}
