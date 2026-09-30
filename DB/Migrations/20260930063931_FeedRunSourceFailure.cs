using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class FeedRunSourceFailure : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "SourceFailure", table: "FeedRuns", type: "TEXT", nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "SourceFailure", table: "FeedRuns");
		}
	}
}
