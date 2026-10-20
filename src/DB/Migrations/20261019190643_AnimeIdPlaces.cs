using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261019190643_AnimeIdPlaces : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(name: "EpisodeOffset", table: "AnimeIds", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<int>(name: "TvdbSeason", table: "AnimeIds", type: "INTEGER", nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "EpisodeOffset", table: "AnimeIds");

		migrationBuilder.DropColumn(name: "TvdbSeason", table: "AnimeIds");
	}
}
