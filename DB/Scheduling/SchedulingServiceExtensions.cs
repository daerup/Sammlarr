using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Scheduling;

public static class SchedulingServiceExtensions
{
	extension(IServiceCollection services)
	{
		public void AddSammlarrScheduling(IConfiguration configuration) => services.AddModule(new SchedulingModule(configuration));

		public void AddSammlarrScheduling(DbConnection connection) => services.AddModule(new SchedulingModule(connection));
	}
}
