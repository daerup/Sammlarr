using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RenameWantlistSinkBlocklist : Migration
	{
		private const string BlocklistColumns = "\"UserId\", \"MediaId\", \"CreatedAt\", \"UpdatedAt\"";

		private const string OutboxColumns = "\"UserId\", \"MediaId\", \"Action\", \"Status\", \"Attempts\", \"AddedAt\"";

		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			CreateBlocklist(migrationBuilder, "Blocklist");
			CreateWantlist(migrationBuilder, "Wantlist", sinkColumn: "Sink");
			CreateOutbox(migrationBuilder, "WantlistOutbox");

			Copy(migrationBuilder, from: "Blacklist", to: "Blocklist", BlocklistColumns);
			CopyWantlist(migrationBuilder, from: new("Watchlist", "RemoteProvider", "Remote"), to: new("Wantlist", "Sink", "Sink"));
			Copy(migrationBuilder, from: "WatchlistOutbox", to: "WantlistOutbox", OutboxColumns);

			migrationBuilder.DropTable(name: "Blacklist");
			migrationBuilder.DropTable(name: "Watchlist");
			migrationBuilder.DropTable(name: "WatchlistOutbox");

			migrationBuilder.RenameColumn(name: "WatchlistProvider", table: "ProviderConfig", newName: "Sink");
			migrationBuilder.RenameColumn(name: "Unresolved", table: "FeedRuns", newName: "Unmatched");

			RenameStoredValues(migrationBuilder, from: ("Watchlist", "Blacklisted"), to: ("Wantlist", "Blocklisted"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			CreateBlocklist(migrationBuilder, "Blacklist");
			CreateWantlist(migrationBuilder, "Watchlist", sinkColumn: "RemoteProvider");
			CreateOutbox(migrationBuilder, "WatchlistOutbox");

			Copy(migrationBuilder, from: "Blocklist", to: "Blacklist", BlocklistColumns);
			CopyWantlist(migrationBuilder, from: new("Wantlist", "Sink", "Sink"), to: new("Watchlist", "RemoteProvider", "Remote"));
			Copy(migrationBuilder, from: "WantlistOutbox", to: "WatchlistOutbox", OutboxColumns);

			migrationBuilder.DropTable(name: "Blocklist");
			migrationBuilder.DropTable(name: "Wantlist");
			migrationBuilder.DropTable(name: "WantlistOutbox");

			migrationBuilder.RenameColumn(name: "Sink", table: "ProviderConfig", newName: "WatchlistProvider");
			migrationBuilder.RenameColumn(name: "Unmatched", table: "FeedRuns", newName: "Unresolved");

			RenameStoredValues(migrationBuilder, from: ("Wantlist", "Blocklisted"), to: ("Watchlist", "Blacklisted"));
		}

		private sealed record WantlistShape(string Table, string SinkColumn, string SinkOrigin);

		private static void CreateBlocklist(MigrationBuilder migrationBuilder, string name)
		{
			migrationBuilder.CreateTable(
				name: name,
				columns: table => new
				{
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey($"PK_{name}", x => new { x.UserId, x.MediaId });
					table.ForeignKey(
						name: $"FK_{name}_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
					table.ForeignKey(
						name: $"FK_{name}_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(name: $"IX_{name}_MediaId", table: name, column: "MediaId");
		}

		private static void CreateWantlist(MigrationBuilder migrationBuilder, string name, string sinkColumn)
		{
			migrationBuilder.CreateTable(
				name: name,
				columns: table => new
				{
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					Origin = table.Column<string>(type: "TEXT", nullable: false),
					Sink = table.Column<string>(name: sinkColumn, type: "TEXT", nullable: true),
					ManualSource = table.Column<string>(type: "TEXT", nullable: true),
					SourceFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
					SourceFeedNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
					SourceRunId = table.Column<Guid>(type: "TEXT", nullable: true),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey($"PK_{name}", x => new { x.UserId, x.MediaId });
					table.ForeignKey(
						name: $"FK_{name}_FeedRuns_SourceRunId",
						column: x => x.SourceRunId,
						principalTable: "FeedRuns",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
					table.ForeignKey(
						name: $"FK_{name}_Feeds_SourceFeedId",
						column: x => x.SourceFeedId,
						principalTable: "Feeds",
						principalColumn: "Id",
						onDelete: ReferentialAction.SetNull
					);
					table.ForeignKey(
						name: $"FK_{name}_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
					table.ForeignKey(
						name: $"FK_{name}_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(name: $"IX_{name}_MediaId", table: name, column: "MediaId");
			migrationBuilder.CreateIndex(name: $"IX_{name}_SourceFeedId", table: name, column: "SourceFeedId");
			migrationBuilder.CreateIndex(name: $"IX_{name}_SourceRunId", table: name, column: "SourceRunId");
		}

		private static void CreateOutbox(MigrationBuilder migrationBuilder, string name)
		{
			migrationBuilder.CreateTable(
				name: name,
				columns: table => new
				{
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					Action = table.Column<string>(type: "TEXT", nullable: false),
					Status = table.Column<string>(type: "TEXT", nullable: false),
					Attempts = table.Column<int>(type: "INTEGER", nullable: false),
					AddedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey($"PK_{name}", x => new { x.UserId, x.MediaId });
					table.ForeignKey(
						name: $"FK_{name}_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
					table.ForeignKey(
						name: $"FK_{name}_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(name: $"IX_{name}_Action", table: name, column: "Action");
			migrationBuilder.CreateIndex(name: $"IX_{name}_MediaId", table: name, column: "MediaId");
		}

		private static void Copy(MigrationBuilder migrationBuilder, string from, string to, string columns) =>
			migrationBuilder.Sql($"""INSERT INTO "{to}" ({columns}) SELECT {columns} FROM "{from}";""");

		private static void CopyWantlist(MigrationBuilder migrationBuilder, WantlistShape from, WantlistShape to)
		{
			const string shared = "\"ManualSource\", \"SourceFeedId\", \"SourceFeedNameSnapshot\", \"SourceRunId\", \"CreatedAt\", \"UpdatedAt\"";
			migrationBuilder.Sql(
				$"""
				INSERT INTO "{to.Table}" ("UserId", "MediaId", "Origin", "{to.SinkColumn}", {shared})
				SELECT "UserId", "MediaId", CASE "Origin" WHEN '{from.SinkOrigin}' THEN '{to.SinkOrigin}' ELSE "Origin" END, "{from.SinkColumn}", {shared}
				FROM "{from.Table}";
				"""
			);
		}

		private static void RenameStoredValues(MigrationBuilder migrationBuilder, (string List, string Blocked) from, (string List, string Blocked) to)
		{
			migrationBuilder.Sql($"""UPDATE "Jobs" SET "Kind" = '{to.List}Sync' WHERE "Kind" = '{from.List}Sync';""");
			migrationBuilder.Sql(
				$"""
				UPDATE "Feeds"
				SET "Limit" = replace(replace("Limit", '"{from.List}ed":', '"{to.List}ed":'), '"{from.Blocked}":', '"{to.Blocked}":')
				WHERE "Limit" IS NOT NULL;
				"""
			);
			RenameEventType(migrationBuilder, from: $"{from.List}SyncCompleted", to: $"{to.List}SyncCompleted");
			RenameEventType(migrationBuilder, from: $"{from.List}SyncFailed", to: $"{to.List}SyncFailed");
		}

		private static void RenameEventType(MigrationBuilder migrationBuilder, string from, string to)
		{
			migrationBuilder.Sql(
				$"""UPDATE "NotificationSubscriptions" SET "EventTypes" = replace("EventTypes", '"{from}"', '"{to}"') WHERE "EventTypes" LIKE '%"{from}"%';"""
			);
			migrationBuilder.Sql($"""UPDATE "NotificationDeliveryOutcomes" SET "EventType" = '{to}' WHERE "EventType" = '{from}';""");
		}
	}
}
