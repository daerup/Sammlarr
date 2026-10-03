using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261003222031_RatingSources : Migration
{
	private const string NewRatingGaps = "ImdbRating, RottenTomatoesCriticsRating, RottenTomatoesAudienceRating";

	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			$"""
			UPDATE "MediaDetails" SET "ConfirmedAbsent" = CASE "ConfirmedAbsent"
			    WHEN 'None' THEN '{NewRatingGaps}'
			    ELSE trim(replace(', ' || "ConfirmedAbsent" || ',', ', Rating,', ', TmdbRating,'), ', ') || ', {NewRatingGaps}'
			END;
			"""
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""DELETE FROM "MediaRatings" WHERE "Source" <> 'Tmdb';""");
		migrationBuilder.Sql(
			"""
			UPDATE "MediaDetails" SET "ConfirmedAbsent" = coalesce(nullif(trim(
			    replace(replace(replace(replace(replace(', ' || "ConfirmedAbsent" || ',',
			        ', Ratings,', ', Rating,'),
			        ', ImdbRating,', ','),
			        ', RottenTomatoesCriticsRating,', ','),
			        ', RottenTomatoesAudienceRating,', ','),
			        ', TmdbRating,', ', Rating,'),
			    ', '), ''), 'None');
			"""
		);
	}
}
