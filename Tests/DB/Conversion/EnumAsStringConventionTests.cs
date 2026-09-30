using DB.Conversion.Enums;
using Domain.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Conversion;

public class EnumAsStringConventionTests
{
	[Test]
	public async Task NullableEnum_IsStoredAsItsStringName()
	{
		await using var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();
		await using var context = new EnumContext(connection);
		await context.Database.EnsureCreatedAsync();

		context.Holders.Add(new EnumHolder { NullableProvider = SinkKind.Plex });
		await context.SaveChangesAsync();

		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT typeof(NullableProvider), NullableProvider FROM Holders";
		await using var reader = await command.ExecuteReaderAsync();
		await reader.ReadAsync();

		await Assert.That(reader.GetString(0)).IsEqualTo("text");
		await Assert.That(reader.GetString(1)).IsEqualTo("Plex");
	}

	[Test]
	public async Task NullableEnum_RoundTripsNullAndValue()
	{
		await using var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();
		await using var context = new EnumContext(connection);
		await context.Database.EnsureCreatedAsync();

		context.Holders.Add(new EnumHolder { NullableProvider = SinkKind.Plex });
		context.Holders.Add(new EnumHolder { NullableProvider = null });
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();

		var loaded = await context.Holders.OrderBy(holder => holder.Id).ToListAsync();

		await Assert.That(loaded[0].NullableProvider).IsEqualTo(SinkKind.Plex);
		await Assert.That(loaded[1].NullableProvider).IsNull();
	}

	private sealed class EnumContext(SqliteConnection connection) : DbContext
	{
		public DbSet<EnumHolder> Holders => Set<EnumHolder>();

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(connection);

		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
			configurationBuilder.Conventions.Add(_ => new EnumAsStringConvention());
	}

	private sealed class EnumHolder
	{
		public int Id { get; set; }
		public SinkKind? NullableProvider { get; set; }
	}
}
