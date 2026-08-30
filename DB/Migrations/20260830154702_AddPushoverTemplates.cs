using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddPushoverTemplates : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "MessageTemplate", table: "NotificationTargets", type: "TEXT", maxLength: 8192, nullable: true);

			migrationBuilder.AddColumn<string>(name: "TitleTemplate", table: "NotificationTargets", type: "TEXT", maxLength: 2048, nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "MessageTemplate", table: "NotificationTargets");

			migrationBuilder.DropColumn(name: "TitleTemplate", table: "NotificationTargets");
		}
	}
}
