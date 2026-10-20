using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261019195205_ShowPartAnswers : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterColumn<Guid>(name: "MediaId", table: "ShowParts", type: "TEXT", nullable: true, oldClrType: typeof(Guid), oldType: "TEXT");

		migrationBuilder.AddColumn<long>(name: "AskedAt", table: "ShowParts", type: "INTEGER", nullable: false, defaultValue: 0L);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "AskedAt", table: "ShowParts");

		migrationBuilder.AlterColumn<Guid>(
			name: "MediaId",
			table: "ShowParts",
			type: "TEXT",
			nullable: false,
			defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
			oldClrType: typeof(Guid),
			oldType: "TEXT",
			oldNullable: true
		);
	}
}
