namespace DB.Encryption;

public interface IDataProtectionService
{
	string Encrypt(string plaintext);

	string Decrypt(string ciphertext);
}
