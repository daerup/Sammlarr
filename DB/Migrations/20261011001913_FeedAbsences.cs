using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011001913_FeedAbsences : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "FeedAbsences",
			columns: table => new
			{
				FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				MissedRuns = table.Column<int>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedAbsences", x => new { x.FeedId, x.MediaId });
				table.ForeignKey(
					name: "FK_FeedAbsences_Feeds_FeedId",
					column: x => x.FeedId,
					principalTable: "Feeds",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedAbsences_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedAbsences_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_FeedAbsences_MediaId", table: "FeedAbsences", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_FeedAbsences_UserId", table: "FeedAbsences", column: "UserId");

		migrationBuilder.Sql("""DELETE FROM "FeedRunItems" WHERE "State" = 'Kept'""");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "FeedAbsences");
	}
}
