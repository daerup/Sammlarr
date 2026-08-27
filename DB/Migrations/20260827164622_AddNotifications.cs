using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddNotifications : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "NotificationTargets",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
					Channel = table.Column<string>(type: "TEXT", nullable: false),
					Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
					Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
					SigningSecret = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
					AppToken = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
					UserKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_NotificationTargets", x => x.Id);
					table.ForeignKey(
						name: "FK_NotificationTargets_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "NotificationSubscriptions",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
					EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
					UserId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
					table.ForeignKey(
						name: "FK_NotificationSubscriptions_NotificationTargets_TargetId",
						column: x => x.TargetId,
						principalTable: "NotificationTargets",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_NotificationSubscriptions_Users_UserId",
						column: x => x.UserId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
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

			migrationBuilder.CreateIndex(name: "IX_NotificationTargets_UserId_Channel", table: "NotificationTargets", columns: new[] { "UserId", "Channel" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "NotificationSubscriptions");

			migrationBuilder.DropTable(name: "NotificationTargets");
		}
	}
}
