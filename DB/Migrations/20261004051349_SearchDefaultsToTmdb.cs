using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004051349_SearchDefaultsToTmdb : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("UPDATE \"ProviderConfig\" SET \"SearchProvider\" = 'Tmdb' WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("UPDATE \"ProviderConfig\" SET \"SearchProvider\" = 'Plex' WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
	}
}
