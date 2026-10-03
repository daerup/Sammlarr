using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class MediaDetailsPlaceholdersToNull : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				$"""
				UPDATE "MediaDetails" SET
				    "Title" = {NullIfBlank("Title")},
				    "Tagline" = {NullIfBlank("Tagline")},
				    "Summary" = {NullIfBlank("Summary")},
				    "PosterUrl" = {NullIfBlank("PosterUrl")},
				    "BackdropUrl" = {NullIfBlank("BackdropUrl")},
				    "ReleaseDate" = NULLIF("ReleaseDate", '0001-01-01'),
				    "Runtime" = NULLIF("Runtime", '00:00:00'),
				    "Seasons" = NULLIF("Seasons", 0),
				    "Episodes" = NULLIF("Episodes", 0),
				    "RuntimePerEpisode" = NULLIF("RuntimePerEpisode", '00:00:00');
				"""
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE "MediaDetails" SET
				    "Title" = COALESCE("Title", ''),
				    "Tagline" = COALESCE("Tagline", ''),
				    "Summary" = COALESCE("Summary", ''),
				    "PosterUrl" = COALESCE("PosterUrl", ''),
				    "BackdropUrl" = COALESCE("BackdropUrl", ''),
				    "ReleaseDate" = COALESCE("ReleaseDate", '0001-01-01'),
				    "Runtime" = CASE WHEN "MediaType" = 'Movie' THEN COALESCE("Runtime", '00:00:00') ELSE "Runtime" END,
				    "Seasons" = CASE WHEN "MediaType" = 'Show' THEN COALESCE("Seasons", 0) ELSE "Seasons" END,
				    "Episodes" = CASE WHEN "MediaType" = 'Show' THEN COALESCE("Episodes", 0) ELSE "Episodes" END,
				    "RuntimePerEpisode" = CASE WHEN "MediaType" = 'Show' THEN COALESCE("RuntimePerEpisode", '00:00:00') ELSE "RuntimePerEpisode" END;
				"""
			);
		}

		private static string NullIfBlank(string column)
		{
			return $"""CASE WHEN TRIM("{column}", ' ' || char(9) || char(10) || char(13)) = '' THEN NULL ELSE "{column}" END""";
		}
	}
}
