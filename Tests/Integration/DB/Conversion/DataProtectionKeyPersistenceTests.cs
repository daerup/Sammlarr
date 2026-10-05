using DB.Conversion.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Tests.Conversion;

public class DataProtectionKeyPersistenceTests
{
	private static IDataProtectionService CreatePersistingService(DirectoryInfo keyRing)
	{
		var services = new ServiceCollection();
		services.AddDataProtection().PersistKeysToFileSystem(keyRing).SetApplicationName("Sammlarr");
		var provider = services.BuildServiceProvider();
		return new DataProtectionService(provider.GetRequiredService<IDataProtectionProvider>());
	}

	[Test]
	public async Task PersistedKeys_SurviveANewProvider_AndStillDecrypt()
	{
		var keyRing = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sammlarr-dp-" + Guid.NewGuid().ToString("N")));
		keyRing.Create();
		try
		{
			var plaintext = "provider-access-token";
			var encrypted = CreatePersistingService(keyRing).Encrypt(plaintext);

			var afterRestart = CreatePersistingService(keyRing).Decrypt(encrypted);

			await Assert.That(afterRestart).IsEqualTo(plaintext);
		}
		finally
		{
			keyRing.Delete(recursive: true);
		}
	}

	[Test]
	[NotInParallel]
	public async Task Resolve_HonorsConfiguredPath_AndCreatesIt()
	{
		var path = Path.Combine(Path.GetTempPath(), "sammlarr-dp-" + Guid.NewGuid().ToString("N"));
		Environment.SetEnvironmentVariable("Sammlarr__DataProtection__KeyRingPath", path);
		try
		{
			var directory = DataProtectionKeyRing.Resolve();

			await Assert.That(directory.FullName).IsEqualTo(new DirectoryInfo(path).FullName);
			await Assert.That(directory.Exists).IsTrue();
		}
		finally
		{
			Environment.SetEnvironmentVariable("Sammlarr__DataProtection__KeyRingPath", null);
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
	}
}
