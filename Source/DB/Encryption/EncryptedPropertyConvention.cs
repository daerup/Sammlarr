using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DB.Encryption;

internal sealed class EncryptedPropertyConvention(IDataProtectionService dataProtectionService) : IPropertyAddedConvention
{
	public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
	{
		var property = propertyBuilder.Metadata;
		var memberInfo = property.PropertyInfo ?? (MemberInfo?)property.FieldInfo;

		if (memberInfo?.GetCustomAttribute<EncryptedAttribute>() is null)
		{
			return;
		}

		var clrType = property.ClrType;

		if (clrType == typeof(string))
		{
			propertyBuilder.HasConversion(new EncryptedStringConverter(dataProtectionService));
		}
		else if (clrType == typeof(Guid))
		{
			propertyBuilder.HasConversion(new EncryptedGuidConverter(dataProtectionService));
		}
	}
}
