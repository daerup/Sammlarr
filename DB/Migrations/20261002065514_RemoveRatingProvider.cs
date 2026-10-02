using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class RemoveRatingProvider : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "RatingProvider", table: "ProviderConfig");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "RatingProvider", table: "ProviderConfig", type: "TEXT", nullable: false, defaultValue: "");

			migrationBuilder.UpdateData(table: "ProviderConfig", keyColumn: "Id", keyValue: 1, column: "RatingProvider", value: "Tmdb");
		}
	}
}
