using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261005053003_MediaFieldsRatingSources : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			UPDATE MediaDetails SET ConfirmedAbsent =
			  REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ConfirmedAbsent,
			    'RottenTomatoesAudienceRating', 'RottenTomatoesAudience'), 'RottenTomatoesCriticsRating', 'RottenTomatoesCritics'),
			    'ImdbRating', 'Imdb'), 'TmdbRating', 'Tmdb'), 'Ratings', 'Tmdb, Imdb, RottenTomatoesCritics, RottenTomatoesAudience');
			"""
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			UPDATE MediaDetails SET ConfirmedAbsent =
			  REPLACE(REPLACE(REPLACE(REPLACE(ConfirmedAbsent,
			    'RottenTomatoesAudience', 'RottenTomatoesAudienceRating'), 'RottenTomatoesCritics', 'RottenTomatoesCriticsRating'),
			    'Imdb', 'ImdbRating'), 'Tmdb', 'TmdbRating');
			"""
		);
	}
}
