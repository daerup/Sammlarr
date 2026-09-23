using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddJobsTable : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Jobs",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
					Kind = table.Column<string>(type: "TEXT", nullable: false),
					Key = table.Column<string>(type: "TEXT", nullable: false),
					State = table.Column<string>(type: "TEXT", nullable: false),
					RunAfter = table.Column<long>(type: "INTEGER", nullable: false),
					FirstArmedAt = table.Column<long>(type: "INTEGER", nullable: false),
					Attempts = table.Column<int>(type: "INTEGER", nullable: false),
					LeaseExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Jobs", x => x.Id);
					table.ForeignKey(
						name: "FK_Jobs_Users_OwnerId",
						column: x => x.OwnerId,
						principalTable: "Users",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(
				name: "IX_Jobs_Kind_Key_OwnerId",
				table: "Jobs",
				columns: new[] { "Kind", "Key", "OwnerId" },
				unique: true,
				filter: "\"State\" = 'Queued'"
			);

			migrationBuilder.CreateIndex(name: "IX_Jobs_OwnerId", table: "Jobs", column: "OwnerId");

			migrationBuilder.CreateIndex(name: "IX_Jobs_State_RunAfter", table: "Jobs", columns: new[] { "State", "RunAfter" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "Jobs");
		}
	}
}
