using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddFeedRunLog : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(name: "SyncMode", table: "Feeds", newName: "Trigger");

			migrationBuilder.CreateTable(
				name: "FeedRuns",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
					StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
					DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
					Strategy = table.Column<string>(type: "TEXT", nullable: false),
					Trigger = table.Column<string>(type: "TEXT", nullable: false),
					Conclusiveness = table.Column<string>(type: "TEXT", nullable: false),
					Total = table.Column<int>(type: "INTEGER", nullable: false),
					Added = table.Column<int>(type: "INTEGER", nullable: false),
					Kept = table.Column<int>(type: "INTEGER", nullable: false),
					Bypassed = table.Column<int>(type: "INTEGER", nullable: false),
					Filtered = table.Column<int>(type: "INTEGER", nullable: false),
					Skipped = table.Column<int>(type: "INTEGER", nullable: false),
					Unresolved = table.Column<int>(type: "INTEGER", nullable: false),
					Removed = table.Column<int>(type: "INTEGER", nullable: false),
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FeedRuns", x => x.Id);
					table.ForeignKey(
						name: "FK_FeedRuns_Feeds_FeedId",
						column: x => x.FeedId,
						principalTable: "Feeds",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_FeedRuns_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "FeedRunItems",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					State = table.Column<string>(type: "TEXT", nullable: false),
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_FeedRunItems", x => x.Id);
					table.ForeignKey(
						name: "FK_FeedRunItems_FeedRuns_FeedRunId",
						column: x => x.FeedRunId,
						principalTable: "FeedRuns",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_FeedRunItems_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(name: "IX_FeedRunItems_FeedRunId_MediaId", table: "FeedRunItems", columns: new[] { "FeedRunId", "MediaId" }, unique: true);

			migrationBuilder.CreateIndex(name: "IX_FeedRunItems_UserId", table: "FeedRunItems", column: "UserId");

			migrationBuilder.CreateIndex(name: "IX_FeedRuns_FeedId_StartedAt", table: "FeedRuns", columns: new[] { "FeedId", "StartedAt" });

			migrationBuilder.CreateIndex(name: "IX_FeedRuns_UserId", table: "FeedRuns", column: "UserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "FeedRunItems");

			migrationBuilder.DropTable(name: "FeedRuns");

			migrationBuilder.RenameColumn(name: "Trigger", table: "Feeds", newName: "SyncMode");
		}
	}
}
