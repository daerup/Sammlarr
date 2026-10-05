using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005055551_ProviderAnswers : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn(name: "FetchedAt", table: "MediaDetails", newName: "RefreshedAt");

		migrationBuilder.AddColumn<long>(name: "AskedAt", table: "MediaDetails", type: "INTEGER", nullable: false, defaultValue: 0L);

		migrationBuilder.Sql("UPDATE MediaDetails SET AskedAt = RefreshedAt;");

		migrationBuilder.DropColumn(name: "ConfirmedAbsent", table: "MediaDetails");

		migrationBuilder.CreateTable(
			name: "ProviderAnswers",
			columns: table => new
			{
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Unfilled = table.Column<string>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ProviderAnswers", x => new { x.MediaId, x.Provider });
				table.ForeignKey(
					name: "FK_ProviderAnswers_MediaDetails_MediaId",
					column: x => x.MediaId,
					principalTable: "MediaDetails",
					principalColumn: "MediaId",
					onDelete: ReferentialAction.Cascade
				);
			}
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ProviderAnswers");

		migrationBuilder.DropColumn(name: "AskedAt", table: "MediaDetails");

		migrationBuilder.RenameColumn(name: "RefreshedAt", table: "MediaDetails", newName: "FetchedAt");

		migrationBuilder.AddColumn<string>(name: "ConfirmedAbsent", table: "MediaDetails", type: "TEXT", nullable: false, defaultValue: "");
	}
}
