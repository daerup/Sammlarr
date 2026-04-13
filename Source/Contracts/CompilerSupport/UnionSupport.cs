// These types are required by the C# compiler to support the 'union' keyword.
// They are NOT yet included in the .NET 11 Preview 2 runtime and must be declared manually.
// They will be removed once a future .NET 11 preview ships them in System.Runtime.CompilerServices.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class UnionAttribute : Attribute;

public interface IUnion
{
	object? Value { get; }
}
