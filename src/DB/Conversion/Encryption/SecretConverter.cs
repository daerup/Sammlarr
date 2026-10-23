using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.Conversion.Encryption;

internal sealed class SecretConverter(IDataProtector protector)
	: ValueConverter<Secret, string>(secret => protector.Protect(secret.Reveal()), stored => new Secret(protector.Unprotect(stored)));
