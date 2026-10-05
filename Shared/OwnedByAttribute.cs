namespace Shared;

[AttributeUsage(AttributeTargets.Field)]
public sealed class OwnedByAttribute<TOwner>(TOwner owner) : Attribute
	where TOwner : struct, Enum
{
	public TOwner Owner { get; } = owner;
}
