using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006235744_FeedRunUsage : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn(name: "Unmatched", table: "FeedRuns", newName: "UnmatchedNotFound");

		migrationBuilder.AddColumn<int>(name: "UnmatchedCouldNotLookUp", table: "FeedRuns", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.CreateTable(
			name: "FeedRunCalls",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				Calls = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRunCalls", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRunCalls_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunCalls_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(
			name: "IX_FeedRunCalls_FeedRunId_Service_Outcome",
			table: "FeedRunCalls",
			columns: new[] { "FeedRunId", "Service", "Outcome" },
			unique: true
		);

		migrationBuilder.CreateIndex(name: "IX_FeedRunCalls_UserId", table: "FeedRunCalls", column: "UserId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "FeedRunCalls");

		migrationBuilder.DropColumn(name: "UnmatchedCouldNotLookUp", table: "FeedRuns");

		migrationBuilder.RenameColumn(name: "UnmatchedNotFound", table: "FeedRuns", newName: "Unmatched");
	}
}
