using DB.Conversion.Encryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace Integration.Tests.DB.Conversion;

public class DataProtectionKeyPersistenceTests
{
	private static ServiceProvider Sammlarr(DirectoryInfo folder)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new SammlarOptions { ConnectionString = $"Data Source={Path.Combine(folder.FullName, "sammlarr.db")}" });
		services.AddModule<EncryptionModule>();
		return services.BuildServiceProvider();
	}

	[Test]
	[NotInParallel]
	public async Task PersistedKeys_SurviveARestart_AndStillDecrypt()
	{
		var folder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sammlarr-dp-" + Guid.NewGuid().ToString("N")));
		folder.Create();
		try
		{
			var plaintext = "provider-access-token";
			string encrypted;
			using (var before = Sammlarr(folder))
			{
				encrypted = before.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect(plaintext);
			}

			using var afterRestart = Sammlarr(folder);

			await Assert.That(afterRestart.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(encrypted)).IsEqualTo(plaintext);
		}
		finally
		{
			folder.Delete(recursive: true);
		}
	}

	[Test]
	[NotInParallel]
	public async Task Protect_WithoutAConfiguredPath_KeepsTheKeyRingBesideTheDatabase()
	{
		var folder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sammlarr-dp-" + Guid.NewGuid().ToString("N")));
		folder.Create();
		try
		{
			using var provider = Sammlarr(folder);

			provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("provider-access-token");

			await Assert.That(Directory.EnumerateFiles(Path.Combine(folder.FullName, "dp-keys"), "key-*.xml")).IsNotEmpty();
		}
		finally
		{
			folder.Delete(recursive: true);
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
			var directory = DataProtectionKeyRing.Resolve("Data Source=sammlarr.db");

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
