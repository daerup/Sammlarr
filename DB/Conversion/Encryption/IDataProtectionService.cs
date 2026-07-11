namespace DB.Conversion.Encryption;

internal interface IDataProtectionService
{
	string Encrypt(string plaintext);

	string Decrypt(string ciphertext);
}
