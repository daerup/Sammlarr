using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class MediaConfirmedMissing : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "ConfirmedMissing", table: "MediaMetadata", type: "TEXT", nullable: false, defaultValue: "None");
			migrationBuilder.Sql(
				$"""
				UPDATE "MediaMetadata" SET "ConfirmedMissing" = CASE
					WHEN {Blank("Tagline")} AND {Blank("BackdropUrl")} THEN 'Tagline, Backdrop'
					WHEN {Blank("Tagline")} THEN 'Tagline'
					WHEN {Blank("BackdropUrl")} THEN 'Backdrop'
					ELSE 'None'
				END
				WHERE NOT {Blank("Title")}
					AND NOT {Blank("Summary")}
					AND NOT {Blank("PosterUrl")}
					AND "ReleaseDate" > '0001-01-01'
					AND "Genres" <> '[]'
					AND ("MediaType" <> 'Movie' OR "Runtime" <> '00:00:00')
					AND ("MediaType" <> 'Show' OR ("Seasons" > 0 AND "Episodes" > 0 AND "RuntimePerEpisode" <> '00:00:00'))
				"""
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(name: "ConfirmedMissing", table: "MediaMetadata");
		}

		private static string Blank(string column) => $"trim(\"{column}\", ' ' || char(9) || char(10) || char(13)) = ''";
	}
}
