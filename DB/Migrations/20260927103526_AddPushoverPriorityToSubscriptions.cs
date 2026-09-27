using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddPushoverPriorityToSubscriptions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "Priority", table: "NotificationSubscriptions", type: "TEXT", nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "Priority", table: "NotificationSubscriptions");
		}
	}
}
