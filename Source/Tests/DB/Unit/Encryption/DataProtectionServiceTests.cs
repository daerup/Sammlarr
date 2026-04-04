using DB.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Tests.Unit.Encryption;

public class DataProtectionServiceTests
{
	private IDataProtectionService CreateDataProtectionService()
	{
		var services = new ServiceCollection();
		services.AddDataProtection();
		var provider = services.BuildServiceProvider();
		return new DataProtectionService(provider.GetRequiredService<IDataProtectionProvider>());
	}

	[Test]
	public async Task Encrypt_WithValidString_ReturnsBase64EncodedCiphertext()
	{
		var service = CreateDataProtectionService();
		var plaintext = "super-secret-token";

		var encrypted = service.Encrypt(plaintext);

		await Assert.That(encrypted).IsNotEmpty();
		await Assert.That(encrypted).IsNotEqualTo(plaintext);
	}

	[Test]
	public async Task Encrypt_WithEmptyString_ReturnsEmptyString()
	{
		var service = CreateDataProtectionService();

		var encrypted = service.Encrypt("");

		await Assert.That(encrypted).IsEmpty();
	}

	[Test]
	public async Task Decrypt_WithValidCiphertext_ReturnsOriginalPlaintext()
	{
		var service = CreateDataProtectionService();
		var plaintext = "api-key-12345";
		var encrypted = service.Encrypt(plaintext);

		var decrypted = service.Decrypt(encrypted);

		await Assert.That(decrypted).IsEqualTo(plaintext);
	}

	[Test]
	public async Task Encrypt_ThenDecrypt_WithVariousValues_RoundTripSucceeds()
	{
		var service = CreateDataProtectionService();
		string[] testValues = ["simple-text", "text-with-spaces ", "special!@#$%^&*()", "unicode-café-naïve", "very-long-" + new string('x', 1000)];

		using var _ = Assert.Multiple();
		foreach (var value in testValues)
		{
			var encrypted = service.Encrypt(value);
			var decrypted = service.Decrypt(encrypted);
			await Assert.That(decrypted).IsEqualTo(value);
		}
	}

	[Test]
	public async Task Decrypt_WithInvalidBase64_ThrowsInvalidOperationException()
	{
		var service = CreateDataProtectionService();

		await Assert.That(() => service.Decrypt("not-valid-base64!!!")).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Encrypt_WithSamePlaintext_ProducesDifferentCiphertexts()
	{
		var service1 = CreateDataProtectionService();
		var service2 = CreateDataProtectionService();
		var plaintext = "same-value";

		var encrypted1 = service1.Encrypt(plaintext);
		var encrypted2 = service2.Encrypt(plaintext);

		await Assert.That(encrypted1).IsNotEqualTo(encrypted2);
	}

	[Test]
	public async Task Decrypt_WithEmptyString_ReturnsEmptyString()
	{
		var service = CreateDataProtectionService();

		var decrypted = service.Decrypt("");

		await Assert.That(decrypted).IsEmpty();
	}
}
