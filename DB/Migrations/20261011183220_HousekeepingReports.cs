using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011183220_HousekeepingReports : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "HousekeepingReports",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
				FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
				BytesBefore = table.Column<long>(type: "INTEGER", nullable: true),
				BytesAfter = table.Column<long>(type: "INTEGER", nullable: true),
				Vacuumed = table.Column<bool>(type: "INTEGER", nullable: true),
				FailedStep = table.Column<string>(type: "TEXT", nullable: true),
				FailureReason = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_HousekeepingReports", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "HousekeepingReportSteps",
			columns: table => new
			{
				ReportId = table.Column<int>(type: "INTEGER", nullable: false),
				Step = table.Column<string>(type: "TEXT", nullable: false),
				Removed = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_HousekeepingReportSteps", x => new { x.ReportId, x.Step });
				table.ForeignKey(
					name: "FK_HousekeepingReportSteps_HousekeepingReports_ReportId",
					column: x => x.ReportId,
					principalTable: "HousekeepingReports",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "HousekeepingReportSteps");

		migrationBuilder.DropTable(name: "HousekeepingReports");
	}
}
