namespace Shared;

[AttributeUsage(AttributeTargets.Field)]
public sealed class CapabilitiesAttribute<TCapability>(TCapability capabilities) : Attribute
	where TCapability : struct, Enum
{
	public TCapability Capabilities { get; } = capabilities;
}
