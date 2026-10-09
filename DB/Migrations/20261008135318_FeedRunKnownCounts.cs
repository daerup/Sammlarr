using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261008135318_FeedRunKnownCounts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(name: "Known", table: "FeedRuns", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.AddColumn<int>(name: "NotFoundEarlier", table: "FeedRuns", type: "INTEGER", nullable: false, defaultValue: 0);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "Known", table: "FeedRuns");

		migrationBuilder.DropColumn(name: "NotFoundEarlier", table: "FeedRuns");
	}
}
