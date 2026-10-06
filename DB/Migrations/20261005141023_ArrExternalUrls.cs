using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005141023_ArrExternalUrls : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "Radarr_ExternalUrl", table: "ProviderConfig", type: "TEXT", maxLength: 2048, nullable: true);

		migrationBuilder.AddColumn<string>(name: "Sonarr_ExternalUrl", table: "ProviderConfig", type: "TEXT", maxLength: 2048, nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "Radarr_ExternalUrl", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "Sonarr_ExternalUrl", table: "ProviderConfig");
	}
}
