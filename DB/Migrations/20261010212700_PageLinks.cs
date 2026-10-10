using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261010212700_PageLinks : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "MediaPageLinks",
			columns: table => new
			{
				Site = table.Column<string>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Path = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_MediaPageLinks", x => new { x.MediaId, x.Site });
				table.ForeignKey(
					name: "FK_MediaPageLinks_MediaDetails_MediaId",
					column: x => x.MediaId,
					principalTable: "MediaDetails",
					principalColumn: "MediaId",
					onDelete: ReferentialAction.Cascade
				);
			}
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "MediaPageLinks");
	}
}
