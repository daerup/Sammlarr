using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011194729_DatasetChecks : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "DatasetChecks",
			columns: table => new
			{
				Name = table.Column<string>(type: "TEXT", nullable: false),
				StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
				FinishedAt = table.Column<long>(type: "INTEGER", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				Failure = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DatasetChecks", x => x.Name);
			}
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "DatasetChecks");
	}
}
