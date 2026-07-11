namespace Shared;

[AttributeUsage(AttributeTargets.Field)]
public sealed class ConvertsToAttribute<TCapability>(TCapability targets) : Attribute
	where TCapability : struct, Enum
{
	public TCapability Targets { get; } = targets;
}
