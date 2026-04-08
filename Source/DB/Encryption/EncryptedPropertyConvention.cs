using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Encryption;

internal sealed class EncryptedPropertyConvention(IDataProtectionService dataProtectionService) : IPropertyAddedConvention
{
	public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
	{
		var memberInfo = propertyBuilder.Metadata.PropertyInfo ?? (MemberInfo?)propertyBuilder.Metadata.FieldInfo;
		if (memberInfo?.GetCustomAttribute<EncryptedAttribute>() is null)
		{
			return;
		}

		var converter = GetConverter(propertyBuilder.Metadata.ClrType);
		if (converter is not null)
		{
			propertyBuilder.HasConversion(converter);
		}
	}

	private ValueConverter? GetConverter(Type clrType)
	{
		return clrType switch
		{
			_ when clrType == typeof(string) => new EncryptedStringConverter(dataProtectionService),
			_ when clrType == typeof(Guid) => new EncryptedGuidConverter(dataProtectionService),
			_ => null,
		};
	}
}
