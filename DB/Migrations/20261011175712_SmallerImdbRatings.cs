using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011175712_SmallerImdbRatings : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ImdbRatings");

		migrationBuilder.CreateTable(
			name: "ImdbRatings",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				Score = table.Column<byte>(type: "INTEGER", nullable: false),
				Votes = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ImdbRatings", x => x.Id);
			}
		);

		migrationBuilder.Sql("DELETE FROM \"DatasetLoads\" WHERE \"Name\" = 'imdb-ratings';");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ImdbRatings");

		migrationBuilder.Sql(
			"CREATE TABLE \"ImdbRatings\" (\"Tconst\" TEXT NOT NULL CONSTRAINT \"PK_ImdbRatings\" PRIMARY KEY, \"Average\" REAL NOT NULL, \"Votes\" INTEGER NOT NULL) WITHOUT ROWID;"
		);

		migrationBuilder.Sql("DELETE FROM \"DatasetLoads\" WHERE \"Name\" = 'imdb-ratings';");
	}
}
