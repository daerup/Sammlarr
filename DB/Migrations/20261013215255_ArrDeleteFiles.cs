using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261013215255_ArrDeleteFiles : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<bool>(name: "Radarr_DeleteFiles", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<bool>(name: "Sonarr_DeleteFiles", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "Radarr_DeleteFiles", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "Sonarr_DeleteFiles", table: "ProviderConfig");
	}
}
