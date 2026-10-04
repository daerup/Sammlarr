using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004043312_PersonHues : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<double>(name: "Hue", table: "Users", type: "REAL", nullable: false, defaultValue: 0.0);

		migrationBuilder.Sql(
			"""
			UPDATE Users SET Hue = (
			    SELECT (40 + 137.5 * (Ranked.Position - 1)) - 360 * CAST((40 + 137.5 * (Ranked.Position - 1)) / 360 AS INTEGER)
			    FROM (SELECT Id, ROW_NUMBER() OVER (ORDER BY JoinedAt, Id) AS Position FROM Users) AS Ranked
			    WHERE Ranked.Id = Users.Id)
			"""
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "Hue", table: "Users");
	}
}
