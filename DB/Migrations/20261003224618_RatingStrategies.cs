using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261003224618_RatingStrategies : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "RatingStrategy", table: "Feeds", type: "TEXT", nullable: true);

		migrationBuilder.Sql("""UPDATE "Feeds" SET "RatingStrategy" = 'averaged';""");

		migrationBuilder.CreateTable(
			name: "RatingDefaults",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Strategy = table.Column<string>(type: "TEXT", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RatingDefaults", x => x.Id);
				table.ForeignKey(
					name: "FK_RatingDefaults_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_RatingDefaults_UserId", table: "RatingDefaults", column: "UserId", unique: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "RatingDefaults");

		migrationBuilder.DropColumn(name: "RatingStrategy", table: "Feeds");
	}
}
