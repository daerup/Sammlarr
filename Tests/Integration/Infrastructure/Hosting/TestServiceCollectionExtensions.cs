using Integration.Tests.Infrastructure.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Infrastructure.Hosting;

internal static class TestServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		internal void AddModule<TModule>(HttpStub handler, SqliteConnection connection)
			where TModule : ITestServiceModule, new()
		{
			var module = new TModule { Handler = handler, Connection = connection };
			module.RegisterServices(services);
		}
	}
}
