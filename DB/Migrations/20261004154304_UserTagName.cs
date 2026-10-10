using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004154304_UserTagName : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "TagName", table: "Users", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.Sql("UPDATE \"Users\" SET \"TagName\" = \"DisplayName\";");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "TagName", table: "Users");
	}
}
