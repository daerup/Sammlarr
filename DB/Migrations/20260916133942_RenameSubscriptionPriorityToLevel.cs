using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RenameSubscriptionPriorityToLevel : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "Level", table: "NotificationSubscriptions", type: "TEXT", nullable: true);

			migrationBuilder.Sql(
				"""
				UPDATE NotificationSubscriptions
				SET Level = CASE
				    WHEN Priority IS NULL THEN NULL
				    WHEN (SELECT Channel FROM NotificationTargets WHERE NotificationTargets.Id = NotificationSubscriptions.TargetId) = 'Pushover'
				        THEN CASE WHEN Priority >= 1 THEN 'Error' ELSE 'Info' END
				    WHEN Priority <= 0 THEN 'Info'
				    WHEN Priority = 1 THEN 'Warning'
				    ELSE 'Error'
				END
				"""
			);

			migrationBuilder.DropColumn(name: "Priority", table: "NotificationSubscriptions");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(name: "Priority", table: "NotificationSubscriptions", type: "INTEGER", nullable: true);

			migrationBuilder.Sql(
				"""
				UPDATE NotificationSubscriptions
				SET Priority = CASE Level
				    WHEN 'Info' THEN 0
				    WHEN 'Warning' THEN 1
				    WHEN 'Error' THEN 2
				END
				"""
			);

			migrationBuilder.DropColumn(name: "Level", table: "NotificationSubscriptions");
		}
	}
}
