using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261013183218_WantlistOutboxSink : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropPrimaryKey(name: "PK_WantlistOutbox", table: "WantlistOutbox");

		migrationBuilder.AddColumn<string>(name: "Sink", table: "WantlistOutbox", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddPrimaryKey(name: "PK_WantlistOutbox", table: "WantlistOutbox", columns: new[] { "UserId", "MediaId", "Sink" });
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropPrimaryKey(name: "PK_WantlistOutbox", table: "WantlistOutbox");

		migrationBuilder.DropColumn(name: "Sink", table: "WantlistOutbox");

		migrationBuilder.AddPrimaryKey(name: "PK_WantlistOutbox", table: "WantlistOutbox", columns: new[] { "UserId", "MediaId" });
	}
}
