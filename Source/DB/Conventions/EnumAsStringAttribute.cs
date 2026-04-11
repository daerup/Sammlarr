using System;

namespace DB.Conventions;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EnumAsStringAttribute : Attribute;
