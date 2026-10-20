using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261019192510_ShowParts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "ShowParts",
			columns: table => new
			{
				Source = table.Column<string>(type: "TEXT", nullable: false),
				Id = table.Column<string>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				TvdbSeason = table.Column<int>(type: "INTEGER", nullable: true),
				EpisodeOffset = table.Column<int>(type: "INTEGER", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ShowParts", x => new { x.Id, x.Source });
				table.ForeignKey(
					name: "FK_ShowParts_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_ShowParts_MediaId", table: "ShowParts", column: "MediaId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ShowParts");
	}
}
