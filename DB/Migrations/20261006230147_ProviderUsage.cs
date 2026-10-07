using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261006230147_ProviderUsage : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "RemoteAttempts",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Hour = table.Column<long>(type: "INTEGER", nullable: false),
				Cause = table.Column<string>(type: "TEXT", nullable: false),
				FeedId = table.Column<Guid>(type: "TEXT", nullable: true),
				Attempts = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteAttempts", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "RemoteUsage",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Hour = table.Column<long>(type: "INTEGER", nullable: false),
				Cause = table.Column<string>(type: "TEXT", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				FeedId = table.Column<Guid>(type: "TEXT", nullable: true),
				Calls = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteUsage", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "RemoteAttemptBuckets",
			columns: table => new
			{
				UpToMs = table.Column<int>(type: "INTEGER", nullable: false),
				AttemptId = table.Column<long>(type: "INTEGER", nullable: false),
				Count = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteAttemptBuckets", x => new { x.AttemptId, x.UpToMs });
				table.ForeignKey(
					name: "FK_RemoteAttemptBuckets_RemoteAttempts_AttemptId",
					column: x => x.AttemptId,
					principalTable: "RemoteAttempts",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_RemoteAttempts_Hour", table: "RemoteAttempts", column: "Hour");

		migrationBuilder.CreateIndex(name: "IX_RemoteUsage_Hour", table: "RemoteUsage", column: "Hour");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "RemoteAttemptBuckets");

		migrationBuilder.DropTable(name: "RemoteUsage");

		migrationBuilder.DropTable(name: "RemoteAttempts");
	}
}
