using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DB.Conversion.Encryption;

internal sealed class SecretConvention(IDataProtectionProvider protection) : IPropertyAddedConvention
{
	private readonly SecretConverter _converter = new(protection.CreateProtector("Sammlarr.Secrets"));

	public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
	{
		if (propertyBuilder.Metadata.ClrType == typeof(Secret))
		{
			propertyBuilder.HasConversion(_converter);
		}
	}
}
