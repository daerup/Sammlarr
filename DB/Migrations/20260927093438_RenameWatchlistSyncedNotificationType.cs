using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RenameWatchlistSyncedNotificationType : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			Rename(migrationBuilder, from: "WatchlistSynced", to: "WatchlistSyncCompleted");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			Rename(migrationBuilder, from: "WatchlistSyncCompleted", to: "WatchlistSynced");
		}

		private static void Rename(MigrationBuilder migrationBuilder, string from, string to)
		{
			migrationBuilder.Sql(
				$"""UPDATE "NotificationSubscriptions" SET "EventTypes" = replace("EventTypes", '"{from}"', '"{to}"') WHERE "EventTypes" LIKE '%"{from}"%';"""
			);
			migrationBuilder.Sql($"""UPDATE "NotificationDeliveryOutcomes" SET "EventType" = '{to}' WHERE "EventType" = '{from}';""");
		}
	}
}
