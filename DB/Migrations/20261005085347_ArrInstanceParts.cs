using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005085347_ArrInstanceParts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn(name: "SonarrUrl", table: "ProviderConfig", newName: "Sonarr_Url");

		migrationBuilder.RenameColumn(name: "SonarrSearchOnAdd", table: "ProviderConfig", newName: "Sonarr_SearchOnAdd");

		migrationBuilder.RenameColumn(name: "SonarrRootFolderPath", table: "ProviderConfig", newName: "Sonarr_RootFolderPath");

		migrationBuilder.RenameColumn(name: "SonarrQualityProfileId", table: "ProviderConfig", newName: "Sonarr_QualityProfileId");

		migrationBuilder.RenameColumn(name: "SonarrMonitored", table: "ProviderConfig", newName: "Sonarr_Monitored");

		migrationBuilder.RenameColumn(name: "SonarrApiKey", table: "ProviderConfig", newName: "Sonarr_ApiKey");

		migrationBuilder.RenameColumn(name: "RadarrUrl", table: "ProviderConfig", newName: "Radarr_Url");

		migrationBuilder.RenameColumn(name: "RadarrSearchOnAdd", table: "ProviderConfig", newName: "Radarr_SearchOnAdd");

		migrationBuilder.RenameColumn(name: "RadarrRootFolderPath", table: "ProviderConfig", newName: "Radarr_RootFolderPath");

		migrationBuilder.RenameColumn(name: "RadarrQualityProfileId", table: "ProviderConfig", newName: "Radarr_QualityProfileId");

		migrationBuilder.RenameColumn(name: "RadarrMonitored", table: "ProviderConfig", newName: "Radarr_Monitored");

		migrationBuilder.RenameColumn(name: "RadarrApiKey", table: "ProviderConfig", newName: "Radarr_ApiKey");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn(name: "Sonarr_Url", table: "ProviderConfig", newName: "SonarrUrl");

		migrationBuilder.RenameColumn(name: "Sonarr_SearchOnAdd", table: "ProviderConfig", newName: "SonarrSearchOnAdd");

		migrationBuilder.RenameColumn(name: "Sonarr_RootFolderPath", table: "ProviderConfig", newName: "SonarrRootFolderPath");

		migrationBuilder.RenameColumn(name: "Sonarr_QualityProfileId", table: "ProviderConfig", newName: "SonarrQualityProfileId");

		migrationBuilder.RenameColumn(name: "Sonarr_Monitored", table: "ProviderConfig", newName: "SonarrMonitored");

		migrationBuilder.RenameColumn(name: "Sonarr_ApiKey", table: "ProviderConfig", newName: "SonarrApiKey");

		migrationBuilder.RenameColumn(name: "Radarr_Url", table: "ProviderConfig", newName: "RadarrUrl");

		migrationBuilder.RenameColumn(name: "Radarr_SearchOnAdd", table: "ProviderConfig", newName: "RadarrSearchOnAdd");

		migrationBuilder.RenameColumn(name: "Radarr_RootFolderPath", table: "ProviderConfig", newName: "RadarrRootFolderPath");

		migrationBuilder.RenameColumn(name: "Radarr_QualityProfileId", table: "ProviderConfig", newName: "RadarrQualityProfileId");

		migrationBuilder.RenameColumn(name: "Radarr_Monitored", table: "ProviderConfig", newName: "RadarrMonitored");

		migrationBuilder.RenameColumn(name: "Radarr_ApiKey", table: "ProviderConfig", newName: "RadarrApiKey");
	}
}
