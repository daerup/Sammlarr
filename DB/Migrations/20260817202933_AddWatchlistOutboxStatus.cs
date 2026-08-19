using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddWatchlistOutboxStatus : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "Status", table: "WatchlistOutbox", type: "TEXT", nullable: false, defaultValue: "Pending");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "Status", table: "WatchlistOutbox");
		}
	}
}
