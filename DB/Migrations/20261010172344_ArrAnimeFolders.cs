using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261010172344_ArrAnimeFolders : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(name: "Radarr_AnimeQualityProfileId", table: "ProviderConfig", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "Radarr_AnimeRootFolderPath", table: "ProviderConfig", type: "TEXT", maxLength: 1024, nullable: true);

		migrationBuilder.AddColumn<int>(name: "Sonarr_AnimeQualityProfileId", table: "ProviderConfig", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "Sonarr_AnimeRootFolderPath", table: "ProviderConfig", type: "TEXT", maxLength: 1024, nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "Radarr_AnimeQualityProfileId", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "Radarr_AnimeRootFolderPath", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "Sonarr_AnimeQualityProfileId", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "Sonarr_AnimeRootFolderPath", table: "ProviderConfig");
	}
}
