namespace Shared;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SuppliedByAttribute<TSupplier>(params TSupplier[] suppliers) : Attribute
	where TSupplier : struct, Enum
{
	public IReadOnlyList<TSupplier> Suppliers { get; } = suppliers;
}
