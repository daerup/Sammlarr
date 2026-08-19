using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RenameWatchlistOutboxKindToAction : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(name: "Kind", table: "WatchlistOutbox", newName: "Action");

			migrationBuilder.RenameIndex(name: "IX_WatchlistOutbox_Kind", table: "WatchlistOutbox", newName: "IX_WatchlistOutbox_Action");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(name: "Action", table: "WatchlistOutbox", newName: "Kind");

			migrationBuilder.RenameIndex(name: "IX_WatchlistOutbox_Action", table: "WatchlistOutbox", newName: "IX_WatchlistOutbox_Kind");
		}
	}
}
