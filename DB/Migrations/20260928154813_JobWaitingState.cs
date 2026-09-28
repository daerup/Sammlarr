using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class JobWaitingState : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(name: "IX_Jobs_Kind_Key_OwnerId", table: "Jobs");

			migrationBuilder.CreateIndex(
				name: "IX_Jobs_Kind_Key_OwnerId",
				table: "Jobs",
				columns: new[] { "Kind", "Key", "OwnerId" },
				unique: true,
				filter: "\"State\" IN ('Queued', 'Waiting')"
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(name: "IX_Jobs_Kind_Key_OwnerId", table: "Jobs");

			migrationBuilder.CreateIndex(
				name: "IX_Jobs_Kind_Key_OwnerId",
				table: "Jobs",
				columns: new[] { "Kind", "Key", "OwnerId" },
				unique: true,
				filter: "\"State\" = 'Queued'"
			);
		}
	}
}
