using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class BundleAndPrioritizeSubscriptions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(name: "IX_NotificationSubscriptions_TargetId_EventType", table: "NotificationSubscriptions");

			migrationBuilder.DropIndex(name: "IX_NotificationSubscriptions_UserId_EventType", table: "NotificationSubscriptions");

			migrationBuilder.DropColumn(name: "EventType", table: "NotificationSubscriptions");

			migrationBuilder.AddColumn<string>(name: "EventTypes", table: "NotificationSubscriptions", type: "TEXT", nullable: false, defaultValue: "[]");

			migrationBuilder.AddColumn<int>(name: "Priority", table: "NotificationSubscriptions", type: "INTEGER", nullable: true);

			migrationBuilder.CreateIndex(name: "IX_NotificationSubscriptions_TargetId", table: "NotificationSubscriptions", column: "TargetId");

			migrationBuilder.CreateIndex(name: "IX_NotificationSubscriptions_UserId", table: "NotificationSubscriptions", column: "UserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(name: "IX_NotificationSubscriptions_TargetId", table: "NotificationSubscriptions");

			migrationBuilder.DropIndex(name: "IX_NotificationSubscriptions_UserId", table: "NotificationSubscriptions");

			migrationBuilder.DropColumn(name: "EventTypes", table: "NotificationSubscriptions");

			migrationBuilder.DropColumn(name: "Priority", table: "NotificationSubscriptions");

			migrationBuilder.AddColumn<string>(
				name: "EventType",
				table: "NotificationSubscriptions",
				type: "TEXT",
				maxLength: 100,
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.CreateIndex(
				name: "IX_NotificationSubscriptions_TargetId_EventType",
				table: "NotificationSubscriptions",
				columns: new[] { "TargetId", "EventType" },
				unique: true
			);

			migrationBuilder.CreateIndex(
				name: "IX_NotificationSubscriptions_UserId_EventType",
				table: "NotificationSubscriptions",
				columns: new[] { "UserId", "EventType" }
			);
		}
	}
}
