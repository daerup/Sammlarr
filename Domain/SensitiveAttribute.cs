namespace Domain;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SensitiveAttribute : Attribute;
