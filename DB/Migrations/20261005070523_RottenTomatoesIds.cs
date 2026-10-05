using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005070523_RottenTomatoesIds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			INSERT OR IGNORE INTO ExternalMediaIds (Source, MediaType, Id, MediaId)
			SELECT DISTINCT 'RottenTomatoes', details.MediaType, ratings.RottenTomatoesPath, ratings.MediaId
			FROM MediaRatings AS ratings
			JOIN MediaDetails AS details ON details.MediaId = ratings.MediaId
			WHERE ratings.RottenTomatoesPath IS NOT NULL;
			"""
		);

		migrationBuilder.DropColumn(name: "RottenTomatoesAskedAt", table: "MediaRatings");

		migrationBuilder.DropColumn(name: "RottenTomatoesPath", table: "MediaRatings");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DELETE FROM ExternalMediaIds WHERE Source = 'RottenTomatoes';");

		migrationBuilder.AddColumn<long>(name: "RottenTomatoesAskedAt", table: "MediaRatings", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "RottenTomatoesPath", table: "MediaRatings", type: "TEXT", maxLength: 256, nullable: true);
	}
}
