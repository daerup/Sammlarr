using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004162805_PersonColours : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<double>(name: "Hue", table: "Users", type: "REAL", nullable: true, oldClrType: typeof(double), oldType: "REAL");

		migrationBuilder.AddColumn<double>(name: "PictureChroma", table: "Users", type: "REAL", nullable: true);

		migrationBuilder.AddColumn<double>(name: "PictureHue", table: "Users", type: "REAL", nullable: true);

		migrationBuilder.AddColumn<string>(name: "PictureUrl", table: "Users", type: "TEXT", maxLength: 2048, nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "PictureChroma", table: "Users");

		migrationBuilder.DropColumn(name: "PictureHue", table: "Users");

		migrationBuilder.DropColumn(name: "PictureUrl", table: "Users");

		migrationBuilder.AlterColumn<double>(
			name: "Hue",
			table: "Users",
			type: "REAL",
			nullable: false,
			defaultValue: 0.0,
			oldClrType: typeof(double),
			oldType: "REAL",
			oldNullable: true
		);
	}
}
