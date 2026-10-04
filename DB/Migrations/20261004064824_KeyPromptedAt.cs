using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004064824_KeyPromptedAt : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(name: "KeyPromptedAt", table: "Users", type: "INTEGER", nullable: true);

		migrationBuilder.Sql("UPDATE \"Users\" SET \"KeyPromptedAt\" = \"JoinedAt\";");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "KeyPromptedAt", table: "Users");
	}
}
