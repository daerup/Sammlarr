using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006070808_AnimeIds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AnimeIds",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Type = table.Column<string>(type: "TEXT", nullable: false),
				AniListId = table.Column<int>(type: "INTEGER", nullable: true),
				MalId = table.Column<int>(type: "INTEGER", nullable: true),
				TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
				TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
				ImdbId = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AnimeIds", x => x.Id);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_AniListId", table: "AnimeIds", column: "AniListId");

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_MalId", table: "AnimeIds", column: "MalId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "AnimeIds");
	}
}
