using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011165556_DeletedFeeds : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "DeletedFeeds",
			columns: table => new
			{
				FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", nullable: false),
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				DeletedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DeletedFeeds", x => x.FeedId);
				table.ForeignKey(
					name: "FK_DeletedFeeds_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_DeletedFeeds_DeletedAt", table: "DeletedFeeds", column: "DeletedAt");

		migrationBuilder.CreateIndex(name: "IX_DeletedFeeds_UserId", table: "DeletedFeeds", column: "UserId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "DeletedFeeds");
	}
}
