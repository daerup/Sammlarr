using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261007212816_FeedRunTimes : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "FeedRunTimes",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				UpToMs = table.Column<int>(type: "INTEGER", nullable: false),
				Attempts = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRunTimes", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRunTimes_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunTimes_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(
			name: "IX_FeedRunTimes_FeedRunId_Service_UpToMs",
			table: "FeedRunTimes",
			columns: new[] { "FeedRunId", "Service", "UpToMs" },
			unique: true
		);

		migrationBuilder.CreateIndex(name: "IX_FeedRunTimes_UserId", table: "FeedRunTimes", column: "UserId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "FeedRunTimes");
	}
}
