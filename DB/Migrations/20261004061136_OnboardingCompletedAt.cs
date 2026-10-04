using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004061136_OnboardingCompletedAt : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(name: "OnboardingCompletedAt", table: "ProviderConfig", type: "INTEGER", nullable: true);

		migrationBuilder.Sql(
			"UPDATE \"ProviderConfig\" SET \"OnboardingCompletedAt\" = (SELECT MIN(\"JoinedAt\") FROM \"Users\") WHERE EXISTS (SELECT 1 FROM \"Users\");"
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "OnboardingCompletedAt", table: "ProviderConfig");
	}
}
