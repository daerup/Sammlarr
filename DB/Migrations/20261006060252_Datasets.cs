using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006060252_Datasets : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "DatasetLoads",
			columns: table => new
			{
				Name = table.Column<string>(type: "TEXT", nullable: false),
				LoadedAt = table.Column<long>(type: "INTEGER", nullable: false),
				ETag = table.Column<string>(type: "TEXT", nullable: true),
				Rows = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DatasetLoads", x => x.Name);
			}
		);

		migrationBuilder.Sql(
			"CREATE TABLE \"ImdbRatings\" (\"Tconst\" TEXT NOT NULL CONSTRAINT \"PK_ImdbRatings\" PRIMARY KEY, \"Average\" REAL NOT NULL, \"Votes\" INTEGER NOT NULL) WITHOUT ROWID;"
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "DatasetLoads");

		migrationBuilder.DropTable(name: "ImdbRatings");
	}
}
