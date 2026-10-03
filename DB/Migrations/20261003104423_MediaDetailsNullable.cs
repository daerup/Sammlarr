using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class MediaDetailsNullable : Migration
	{
		private static readonly string[] Texts = ["Tagline", "Summary", "PosterUrl", "BackdropUrl"];

		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(name: "FK_MediaRatings_MediaMetadata_MediaId", table: "MediaRatings");
			migrationBuilder.DropForeignKey(name: "FK_MediaMetadata_Media_MediaId", table: "MediaMetadata");
			migrationBuilder.DropPrimaryKey(name: "PK_MediaMetadata", table: "MediaMetadata");

			migrationBuilder.RenameTable(name: "MediaMetadata", newName: "MediaDetails");
			migrationBuilder.RenameColumn(name: "LastFetchedAt", table: "MediaDetails", newName: "FetchedAt");

			migrationBuilder.AlterColumn<string>(
				name: "Title",
				table: "MediaDetails",
				type: "TEXT",
				maxLength: 500,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "TEXT",
				oldMaxLength: 500
			);

			foreach (var column in Texts)
			{
				migrationBuilder.AlterColumn<string>(name: column, table: "MediaDetails", type: "TEXT", nullable: true, oldClrType: typeof(string), oldType: "TEXT");
			}

			migrationBuilder.AlterColumn<DateOnly>(
				name: "ReleaseDate",
				table: "MediaDetails",
				type: "TEXT",
				nullable: true,
				oldClrType: typeof(DateOnly),
				oldType: "TEXT"
			);

			migrationBuilder.AddPrimaryKey(name: "PK_MediaDetails", table: "MediaDetails", column: "MediaId");

			migrationBuilder.AddForeignKey(
				name: "FK_MediaDetails_Media_MediaId",
				table: "MediaDetails",
				column: "MediaId",
				principalTable: "Media",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade
			);

			migrationBuilder.AddForeignKey(
				name: "FK_MediaRatings_MediaDetails_MediaId",
				table: "MediaRatings",
				column: "MediaId",
				principalTable: "MediaDetails",
				principalColumn: "MediaId",
				onDelete: ReferentialAction.Cascade
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(name: "FK_MediaRatings_MediaDetails_MediaId", table: "MediaRatings");
			migrationBuilder.DropForeignKey(name: "FK_MediaDetails_Media_MediaId", table: "MediaDetails");
			migrationBuilder.DropPrimaryKey(name: "PK_MediaDetails", table: "MediaDetails");

			migrationBuilder.RenameTable(name: "MediaDetails", newName: "MediaMetadata");
			migrationBuilder.RenameColumn(name: "FetchedAt", table: "MediaMetadata", newName: "LastFetchedAt");

			migrationBuilder.AlterColumn<string>(
				name: "Title",
				table: "MediaMetadata",
				type: "TEXT",
				maxLength: 500,
				nullable: false,
				defaultValue: "",
				oldClrType: typeof(string),
				oldType: "TEXT",
				oldMaxLength: 500,
				oldNullable: true
			);

			foreach (var column in Texts)
			{
				migrationBuilder.AlterColumn<string>(
					name: column,
					table: "MediaMetadata",
					type: "TEXT",
					nullable: false,
					defaultValue: "",
					oldClrType: typeof(string),
					oldType: "TEXT",
					oldNullable: true
				);
			}

			migrationBuilder.AlterColumn<DateOnly>(
				name: "ReleaseDate",
				table: "MediaMetadata",
				type: "TEXT",
				nullable: false,
				defaultValue: new DateOnly(1, 1, 1),
				oldClrType: typeof(DateOnly),
				oldType: "TEXT",
				oldNullable: true
			);

			migrationBuilder.AddPrimaryKey(name: "PK_MediaMetadata", table: "MediaMetadata", column: "MediaId");

			migrationBuilder.AddForeignKey(
				name: "FK_MediaMetadata_Media_MediaId",
				table: "MediaMetadata",
				column: "MediaId",
				principalTable: "Media",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade
			);

			migrationBuilder.AddForeignKey(
				name: "FK_MediaRatings_MediaMetadata_MediaId",
				table: "MediaRatings",
				column: "MediaId",
				principalTable: "MediaMetadata",
				principalColumn: "MediaId",
				onDelete: ReferentialAction.Cascade
			);
		}
	}
}
