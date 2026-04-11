using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conventions;

internal sealed class EnumAsStringPropertyConvention : IPropertyAddedConvention
{
	public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
	{
		var memberInfo = propertyBuilder.Metadata.PropertyInfo ?? (MemberInfo?)propertyBuilder.Metadata.FieldInfo;
		if (memberInfo?.GetCustomAttribute<EnumAsStringAttribute>() is null)
		{
			return;
		}

		var clrType = propertyBuilder.Metadata.ClrType;
		if (clrType.IsEnum)
		{
			var converterType = typeof(EnumToStringConverter<>).MakeGenericType(clrType);
			var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
			propertyBuilder.HasConversion(converter);
		}
	}
}
