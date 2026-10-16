using Contracts;
using DB.Conversion;
using DB.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SammlarrDbContext>
{
	public SammlarrDbContext CreateDbContext(string[] args)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IDateTimeProvider, DesignTimeDateTimeProvider>();
		services.AddModule<ConversionModule>();
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		services.AddModule<DataAccessModule>();
		services.AddScoped<EntityAuditer>();
		services.AddScoped<UserOwnershipStamper>();

		var provider = services.BuildServiceProvider();

		var options = new DbContextOptionsBuilder<SammlarrDbContext>().UseSqlite("Data Source=:memory:").Options;

		return new SammlarrDbContext(
			options,
			provider.GetRequiredService<EntityAuditer>(),
			provider.GetRequiredService<UserOwnershipStamper>(),
			new AnonymousCurrentUser(),
			provider.GetServices<IPropertyAddedConvention>(),
			provider.GetServices<IModelConfiguration>(),
			provider.GetRequiredService<IDateTimeProvider>()
		);
	}

	private sealed class DesignTimeDateTimeProvider : IDateTimeProvider
	{
		public DateTimeOffset Now => default;

		public TimeZoneInfo Zone => TimeZoneInfo.Utc;
	}
}
