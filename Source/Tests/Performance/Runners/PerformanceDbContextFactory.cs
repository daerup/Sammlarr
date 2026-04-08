using Contracts;
using DB;
using DB.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Performance.Runners;

internal static class PerformanceDbContextFactory
{
	public static SammlarrDbContext Create(string dbPath)
	{
		var options = new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite($"Data Source={dbPath}").Options;
		return new SammlarrDbContext(options, new EntityAuditer(), (IPropertyAddedConvention[])[], new BenchmarkDateTimeProvider());
	}
}

internal class BenchmarkDateTimeProvider : IDateTimeProvider
{
	public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
