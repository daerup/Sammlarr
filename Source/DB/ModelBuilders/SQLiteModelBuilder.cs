using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DB.ModelBuilders;

internal abstract class SQLiteModelBuilder : IDbmsModelBuilder
{
	public static string Fullname => "Microsoft.EntityFrameworkCore.Sqlite";

	public static void OnModelCreating(ModelBuilder modelBuilder)
	{
		BuildDateTimeOffset(modelBuilder);
	}

	private static void BuildDateTimeOffset(ModelBuilder modelBuilder)
	{
		// SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
		// here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
		// To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
		// use the DateTimeOffsetToBinaryConverter
		// Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
		// This only supports millisecond precision, but should be sufficient for most use cases.
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			var properties = entityType
				.ClrType.GetProperties()
				.Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?));
			foreach (var property in properties)
			{
				modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion(new DateTimeOffsetToBinaryConverter());
			}
		}
	}
}
