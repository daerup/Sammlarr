using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004010931_ArrSinkConfig : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "ArrTagFeedSource", table: "ProviderConfig", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddColumn<bool>(name: "ArrTagPerFeed", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<bool>(name: "ArrTagPerUser", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<bool>(name: "ArrTagPlainPrefix", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<string>(name: "ArrTagPrefix", table: "ProviderConfig", type: "TEXT", maxLength: 64, nullable: false, defaultValue: "");

		migrationBuilder.AddColumn<string>(name: "ArrTagUserSource", table: "ProviderConfig", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddColumn<string>(name: "RadarrApiKey", table: "ProviderConfig", type: "TEXT", maxLength: 4096, nullable: true);

		migrationBuilder.AddColumn<bool>(name: "RadarrMonitored", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<int>(name: "RadarrQualityProfileId", table: "ProviderConfig", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "RadarrRootFolderPath", table: "ProviderConfig", type: "TEXT", maxLength: 1024, nullable: true);

		migrationBuilder.AddColumn<bool>(name: "RadarrSearchOnAdd", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<string>(name: "RadarrUrl", table: "ProviderConfig", type: "TEXT", maxLength: 2048, nullable: true);

		migrationBuilder.AddColumn<string>(name: "SonarrApiKey", table: "ProviderConfig", type: "TEXT", maxLength: 4096, nullable: true);

		migrationBuilder.AddColumn<bool>(name: "SonarrMonitored", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<int>(name: "SonarrQualityProfileId", table: "ProviderConfig", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "SonarrRootFolderPath", table: "ProviderConfig", type: "TEXT", maxLength: 1024, nullable: true);

		migrationBuilder.AddColumn<bool>(name: "SonarrSearchOnAdd", table: "ProviderConfig", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<string>(name: "SonarrUrl", table: "ProviderConfig", type: "TEXT", maxLength: 2048, nullable: true);

		migrationBuilder.UpdateData(
			table: "ProviderConfig",
			keyColumn: "Id",
			keyValue: 1,
			columns: new[]
			{
				"ArrTagFeedSource",
				"ArrTagPerFeed",
				"ArrTagPerUser",
				"ArrTagPlainPrefix",
				"ArrTagPrefix",
				"ArrTagUserSource",
				"RadarrApiKey",
				"RadarrMonitored",
				"RadarrQualityProfileId",
				"RadarrRootFolderPath",
				"RadarrSearchOnAdd",
				"RadarrUrl",
				"SonarrApiKey",
				"SonarrMonitored",
				"SonarrQualityProfileId",
				"SonarrRootFolderPath",
				"SonarrSearchOnAdd",
				"SonarrUrl",
			},
			values: new object[] { "ListName", true, true, false, "sammlarr", "Name", null, true, null, null, true, null, null, true, null, null, true, null }
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "ArrTagFeedSource", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "ArrTagPerFeed", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "ArrTagPerUser", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "ArrTagPlainPrefix", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "ArrTagPrefix", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "ArrTagUserSource", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrApiKey", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrMonitored", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrQualityProfileId", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrRootFolderPath", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrSearchOnAdd", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "RadarrUrl", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrApiKey", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrMonitored", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrQualityProfileId", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrRootFolderPath", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrSearchOnAdd", table: "ProviderConfig");

		migrationBuilder.DropColumn(name: "SonarrUrl", table: "ProviderConfig");
	}
}
