using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004034632_LocalAccounts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "ConfigResetHash", table: "Users", type: "TEXT", maxLength: 512, nullable: true);

		migrationBuilder.AddColumn<int>(name: "FailedSignIns", table: "Users", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.AddColumn<long>(name: "LockedUntil", table: "Users", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "PasswordHash", table: "Users", type: "TEXT", maxLength: 512, nullable: true);

		migrationBuilder.AddColumn<long>(name: "SessionsRevokedAt", table: "Users", type: "INTEGER", nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "ConfigResetHash", table: "Users");

		migrationBuilder.DropColumn(name: "FailedSignIns", table: "Users");

		migrationBuilder.DropColumn(name: "LockedUntil", table: "Users");

		migrationBuilder.DropColumn(name: "PasswordHash", table: "Users");

		migrationBuilder.DropColumn(name: "SessionsRevokedAt", table: "Users");
	}
}
