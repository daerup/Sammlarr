using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddNotificationDeliveryOutcomes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "NotificationDeliveryOutcomes",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					EventId = table.Column<Guid>(type: "TEXT", nullable: false),
					TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
					EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
					Kind = table.Column<string>(type: "TEXT", nullable: false),
					Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
					NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: true),
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_NotificationDeliveryOutcomes", x => x.Id);
					table.ForeignKey(
						name: "FK_NotificationDeliveryOutcomes_NotificationTargets_TargetId",
						column: x => x.TargetId,
						principalTable: "NotificationTargets",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_NotificationDeliveryOutcomes_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(
				name: "IX_NotificationDeliveryOutcomes_EventId_TargetId",
				table: "NotificationDeliveryOutcomes",
				columns: new[] { "EventId", "TargetId" },
				unique: true
			);

			migrationBuilder.CreateIndex(
				name: "IX_NotificationDeliveryOutcomes_TargetId_UpdatedAt",
				table: "NotificationDeliveryOutcomes",
				columns: new[] { "TargetId", "UpdatedAt" }
			);

			migrationBuilder.CreateIndex(name: "IX_NotificationDeliveryOutcomes_UserId", table: "NotificationDeliveryOutcomes", column: "UserId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "NotificationDeliveryOutcomes");
		}
	}
}
