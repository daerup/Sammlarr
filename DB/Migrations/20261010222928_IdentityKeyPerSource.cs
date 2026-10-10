using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261010222928_IdentityKeyPerSource : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_ExternalMediaIds_MediaId", table: "ExternalMediaIds");

		migrationBuilder.AddColumn<int>(name: "UnmatchedConflict", table: "FeedRuns", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.CreateIndex(name: "IX_ExternalMediaIds_MediaId_Source", table: "ExternalMediaIds", columns: new[] { "MediaId", "Source" }, unique: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_ExternalMediaIds_MediaId_Source", table: "ExternalMediaIds");

		migrationBuilder.DropColumn(name: "UnmatchedConflict", table: "FeedRuns");

		migrationBuilder.CreateIndex(name: "IX_ExternalMediaIds_MediaId", table: "ExternalMediaIds", column: "MediaId");
	}
}
