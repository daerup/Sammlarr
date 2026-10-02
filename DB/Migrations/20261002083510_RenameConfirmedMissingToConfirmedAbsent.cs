using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RenameConfirmedMissingToConfirmedAbsent : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(name: "ConfirmedMissing", table: "MediaMetadata", newName: "ConfirmedAbsent");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.RenameColumn(name: "ConfirmedAbsent", table: "MediaMetadata", newName: "ConfirmedMissing");
		}
	}
}
