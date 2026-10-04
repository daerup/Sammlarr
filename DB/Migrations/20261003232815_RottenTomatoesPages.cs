using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261003232815_RottenTomatoesPages : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(name: "RottenTomatoesAskedAt", table: "MediaRatings", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "RottenTomatoesPath", table: "MediaRatings", type: "TEXT", maxLength: 256, nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "RottenTomatoesAskedAt", table: "MediaRatings");

		migrationBuilder.DropColumn(name: "RottenTomatoesPath", table: "MediaRatings");
	}
}
