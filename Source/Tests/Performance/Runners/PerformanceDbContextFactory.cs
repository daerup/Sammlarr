using DB;
using DB.Encryption;
using Microsoft.EntityFrameworkCore;

namespace Performance.Runners;

internal static class PerformanceDbContextFactory
{
	public static SammlarrDbContext Create(string dbPath)
	{
		var options = new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite($"Data Source={dbPath}").Options;

		return new SammlarrDbContext(options, new EntityAuditer(), new NoOpDataProtectionService());
	}
}

internal class NoOpDataProtectionService : IDataProtectionService
{
	public string Encrypt(string plaintext) => plaintext;

	public string Decrypt(string ciphertext) => ciphertext;
}
