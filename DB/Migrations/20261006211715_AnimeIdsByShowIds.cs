using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006211715_AnimeIdsByShowIds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateIndex(name: "IX_AnimeIds_ImdbId_Type", table: "AnimeIds", columns: new[] { "ImdbId", "Type" });

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_TmdbId_Type", table: "AnimeIds", columns: new[] { "TmdbId", "Type" });
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_AnimeIds_ImdbId_Type", table: "AnimeIds");

		migrationBuilder.DropIndex(name: "IX_AnimeIds_TmdbId_Type", table: "AnimeIds");
	}
}
