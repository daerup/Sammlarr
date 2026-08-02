using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddWatchlistRemoteProvider : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "RemoteProvider", table: "Watchlist", type: "TEXT", nullable: true);

			migrationBuilder.Sql("UPDATE \"Watchlist\" SET \"RemoteProvider\" = 'Plex' WHERE \"Origin\" = 'Remote';");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "RemoteProvider", table: "Watchlist");
		}
	}
}
