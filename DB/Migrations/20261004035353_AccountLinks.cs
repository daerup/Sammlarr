using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004035353_AccountLinks : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AccountLinks",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Kind = table.Column<string>(type: "TEXT", nullable: false),
				Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: true),
				TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
				IssuedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AccountLinks", x => x.Id);
				table.ForeignKey(
					name: "FK_AccountLinks_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_Kind_Username", table: "AccountLinks", columns: new[] { "Kind", "Username" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_TokenHash", table: "AccountLinks", column: "TokenHash", unique: true);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_UserId", table: "AccountLinks", column: "UserId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "AccountLinks");
	}
}
