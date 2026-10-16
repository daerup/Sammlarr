namespace Domain.Wantlist.Arr;

public sealed class ArrAppsUnreachableException(string reason, ValueSet<ArrApp> apps) : Exception(reason)
{
	public ValueSet<ArrApp> Apps { get; } = apps;
}
