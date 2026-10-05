using Domain.Media;

namespace Domain.Wantlist.Arr;

public static class ArrSearchTerm
{
	public static string Of(IExternalMediaId id) => $"{id.Source.ToString().ToLowerInvariant()}:{id.Value}";
}
