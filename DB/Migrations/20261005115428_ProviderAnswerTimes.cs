using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005115428_ProviderAnswerTimes : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(name: "AnsweredAt", table: "ProviderAnswers", type: "INTEGER", nullable: false, defaultValue: 0L);

		migrationBuilder.Sql("UPDATE ProviderAnswers SET AnsweredAt = (SELECT AskedAt FROM MediaDetails WHERE MediaDetails.MediaId = ProviderAnswers.MediaId);");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "AnsweredAt", table: "ProviderAnswers");
	}
}
