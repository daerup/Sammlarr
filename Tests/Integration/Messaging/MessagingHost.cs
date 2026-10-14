using Contracts.Identity;
using Domain.Identity;
using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Options;

namespace Integration.Tests.Messaging;

internal static class MessagingHost
{
	internal static Mock<IUserScope> PassingUserScope()
	{
		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		return userScope;
	}

	internal static async Task WithHostAsync(Func<IHost, Task> body, Action<IServiceCollection>? configureServices = null)
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"wolverine-test-{Guid.NewGuid():N}.db");
		var connectionString = $"Data Source={dbPath}";
		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?>(StringComparer.Ordinal)
					{
						[$"{SammlarOptions.SectionName}:{nameof(SammlarOptions.ConnectionString)}"] = connectionString,
					}
				)
				.Build();

			var builder = Host.CreateDefaultBuilder();
			if (configureServices is not null)
			{
				builder.ConfigureServices(configureServices);
			}

			using var host = await builder.UseSammlarrMessaging(configuration).StartAsync();
			await body(host);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(dbPath))
			{
				File.Delete(dbPath);
			}
		}
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
