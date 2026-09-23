using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace DB;

public sealed class SchedulingModule : IServiceModule
{
	private readonly Action<DbContextOptionsBuilder> _store;

	public SchedulingModule(IConfiguration configuration)
	{
		var connectionString =
			configuration.GetSection(SammlarOptions.SectionName)[nameof(SammlarOptions.ConnectionString)]
			?? throw new InvalidOperationException("Sammlarr:ConnectionString is not configured.");
		_store = options => Configure(options.UseSqlite(connectionString));
	}

	public SchedulingModule(DbConnection connection) => _store = options => Configure(options.UseSqlite(connection));

	public void RegisterServices(IServiceCollection services) =>
		services.AddTickerQ(options =>
		{
			options.ConfigureScheduler(scheduler => scheduler.MaxConcurrency = 1);
			options.AddOperationalStore(store => store.UseTickerQDbContext<TickerQDbContext>(_store));
		});

	private static void Configure(DbContextOptionsBuilder options) =>
		options.AddInterceptors(new SqlitePragmaInterceptor()).ConfigureWarnings(warnings => warnings.Ignore(SqliteEventId.SchemaConfiguredWarning));
}
