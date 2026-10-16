namespace Contracts.DI;

public sealed record Keyed<TKey, TCapability>(TKey Key, TCapability Capability)
	where TKey : struct, Enum;
