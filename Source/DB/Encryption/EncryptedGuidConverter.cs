using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Encryption;

internal class EncryptedGuidConverter(IDataProtectionService dataProtectionService)
	: ValueConverter<Guid, string>(v => dataProtectionService.Encrypt(v.ToString()), v => Guid.Parse(dataProtectionService.Decrypt(v)));
