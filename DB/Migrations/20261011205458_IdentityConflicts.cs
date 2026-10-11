using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261011205458_IdentityConflicts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "IdentityConflicts",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				HeldId = table.Column<Guid>(type: "TEXT", nullable: false),
				OtherId = table.Column<Guid>(type: "TEXT", nullable: false),
				ClaimedSource = table.Column<string>(type: "TEXT", nullable: false),
				ClaimedType = table.Column<string>(type: "TEXT", nullable: false),
				ClaimedId = table.Column<string>(type: "TEXT", nullable: false),
				SeenAt = table.Column<long>(type: "INTEGER", nullable: false),
				Attempts = table.Column<int>(type: "INTEGER", nullable: false),
				AskAfter = table.Column<long>(type: "INTEGER", nullable: false),
				AskedService = table.Column<string>(type: "TEXT", nullable: true),
				AskedOutcome = table.Column<string>(type: "TEXT", nullable: true),
				AskedAt = table.Column<long>(type: "INTEGER", nullable: true),
				Distinct = table.Column<string>(type: "TEXT", nullable: true),
				DistinctAt = table.Column<long>(type: "INTEGER", nullable: true),
				ContradictedBy = table.Column<string>(type: "TEXT", nullable: true),
				AnsweredBy = table.Column<string>(type: "TEXT", nullable: true),
				AnswerSource = table.Column<string>(type: "TEXT", nullable: true),
				AnswerType = table.Column<string>(type: "TEXT", nullable: true),
				AnswerId = table.Column<string>(type: "TEXT", nullable: true),
				KeptApartBy = table.Column<Guid>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_IdentityConflicts", x => x.Id);
				table.ForeignKey(
					name: "FK_IdentityConflicts_Media_HeldId",
					column: x => x.HeldId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_IdentityConflicts_Media_OtherId",
					column: x => x.OtherId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "IdentityConflictSources",
			columns: table => new { ConflictId = table.Column<Guid>(type: "TEXT", nullable: false), Service = table.Column<string>(type: "TEXT", nullable: false) },
			constraints: table =>
			{
				table.PrimaryKey("PK_IdentityConflictSources", x => new { x.ConflictId, x.Service });
				table.ForeignKey(
					name: "FK_IdentityConflictSources_IdentityConflicts_ConflictId",
					column: x => x.ConflictId,
					principalTable: "IdentityConflicts",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_IdentityConflicts_HeldId_OtherId", table: "IdentityConflicts", columns: new[] { "HeldId", "OtherId" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_IdentityConflicts_OtherId", table: "IdentityConflicts", column: "OtherId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "IdentityConflictSources");

		migrationBuilder.DropTable(name: "IdentityConflicts");
	}
}
