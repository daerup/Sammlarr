using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Enums;

internal sealed class EnumAsStringConvention : IPropertyAddedConvention
{
	public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
	{
		var enumType = Nullable.GetUnderlyingType(propertyBuilder.Metadata.ClrType) ?? propertyBuilder.Metadata.ClrType;
		if (!enumType.IsEnum)
		{
			return;
		}

		var converterType = typeof(EnumToStringConverter<>).MakeGenericType(enumType);
		var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
		propertyBuilder.HasConversion(converter);
	}
}
