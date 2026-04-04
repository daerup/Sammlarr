using Microsoft.AspNetCore.DataProtection;

namespace DB.Encryption;

public class DataProtectionService(IDataProtectionProvider dataProtectionProvider) : IDataProtectionService
{
	private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Sammlarr.DB.Encryption");

	public string Encrypt(string plaintext)
	{
		if (string.IsNullOrEmpty(plaintext))
		{
			return plaintext;
		}

		var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
		var ciphertextBytes = _protector.Protect(plaintextBytes);
		return Convert.ToBase64String(ciphertextBytes);
	}

	public string Decrypt(string ciphertext)
	{
		if (string.IsNullOrEmpty(ciphertext))
		{
			return ciphertext;
		}

		try
		{
			var ciphertextBytes = Convert.FromBase64String(ciphertext);
			var plaintextBytes = _protector.Unprotect(ciphertextBytes);
			return System.Text.Encoding.UTF8.GetString(plaintextBytes);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Failed to decrypt data. The data may be corrupted or the protection key has changed.", ex);
		}
	}
}
