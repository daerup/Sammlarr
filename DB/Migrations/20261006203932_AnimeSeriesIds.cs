using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006203932_AnimeSeriesIds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(name: "SeriesTvdbId", table: "AnimeIds", type: "INTEGER", nullable: true);

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_TvdbId", table: "AnimeIds", column: "TvdbId");

		migrationBuilder.Sql("DELETE FROM DatasetLoads WHERE Name = 'anime-ids';");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_AnimeIds_TvdbId", table: "AnimeIds");

		migrationBuilder.DropColumn(name: "SeriesTvdbId", table: "AnimeIds");
	}
}
