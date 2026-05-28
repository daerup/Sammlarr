using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Encryption;

internal class EncryptedGuidConverter(IDataProtectionService dataProtectionService)
	: ValueConverter<Guid, string>(v => dataProtectionService.Encrypt(v.ToString()), v => Guid.Parse(dataProtectionService.Decrypt(v)));
