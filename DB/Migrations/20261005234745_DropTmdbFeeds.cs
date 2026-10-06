using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005234745_DropTmdbFeeds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DELETE FROM CronTickers WHERE Id IN (SELECT Id FROM Feeds WHERE Provider = 'Tmdb');");
		migrationBuilder.Sql("DELETE FROM Feeds WHERE Provider = 'Tmdb';");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder) { }
}
