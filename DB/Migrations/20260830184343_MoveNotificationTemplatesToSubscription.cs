using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class MoveNotificationTemplatesToSubscription : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "BodyTemplate", table: "NotificationTargets");

			migrationBuilder.DropColumn(name: "MessageTemplate", table: "NotificationTargets");

			migrationBuilder.DropColumn(name: "TitleTemplate", table: "NotificationTargets");

			migrationBuilder.AddColumn<string>(name: "BodyTemplate", table: "NotificationSubscriptions", type: "TEXT", maxLength: 8192, nullable: true);

			migrationBuilder.AddColumn<string>(name: "MessageTemplate", table: "NotificationSubscriptions", type: "TEXT", maxLength: 8192, nullable: true);

			migrationBuilder.AddColumn<string>(name: "TitleTemplate", table: "NotificationSubscriptions", type: "TEXT", maxLength: 2048, nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "BodyTemplate", table: "NotificationSubscriptions");

			migrationBuilder.DropColumn(name: "MessageTemplate", table: "NotificationSubscriptions");

			migrationBuilder.DropColumn(name: "TitleTemplate", table: "NotificationSubscriptions");

			migrationBuilder.AddColumn<string>(name: "BodyTemplate", table: "NotificationTargets", type: "TEXT", maxLength: 8192, nullable: true);

			migrationBuilder.AddColumn<string>(name: "MessageTemplate", table: "NotificationTargets", type: "TEXT", maxLength: 8192, nullable: true);

			migrationBuilder.AddColumn<string>(name: "TitleTemplate", table: "NotificationTargets", type: "TEXT", maxLength: 2048, nullable: true);
		}
	}
}
