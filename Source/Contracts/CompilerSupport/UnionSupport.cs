namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UnionAttribute : Attribute;

public interface IUnion
{
	object Value { get; }
}
