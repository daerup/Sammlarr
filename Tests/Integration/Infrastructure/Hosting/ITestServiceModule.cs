using Integration.Tests.Infrastructure.Http;
using Microsoft.Data.Sqlite;
using Shared.DI;

namespace Integration.Tests.Infrastructure.Hosting;

internal interface ITestServiceModule : IServiceModule
{
	HttpStub Handler { set; }
	SqliteConnection Connection { set; }
}
