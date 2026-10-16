using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Encryption;

internal sealed class EncryptedStringConverter(IDataProtectionService dataProtectionService)
	: ValueConverter<string, string>(
		v => string.IsNullOrEmpty(v) ? v : dataProtectionService.Encrypt(v),
		v => string.IsNullOrEmpty(v) ? v : dataProtectionService.Decrypt(v)
	);
