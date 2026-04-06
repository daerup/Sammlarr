using DB;
using Microsoft.EntityFrameworkCore;

namespace Performance.Runners;

internal static class PerformanceDbContextFactory
{
	public static SammlarrDbContext Create(string dbPath)
	{
		var options = new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite($"Data Source={dbPath}").Options;

		return new SammlarrDbContext(options);
	}
}
