using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class AddWatchlistSourceRunForeignKey : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE Watchlist
				SET SourceRunId = (
				    SELECT r.Id FROM FeedRuns r
				    WHERE r.FeedId = Watchlist.SourceFeedId
				    ORDER BY r.StartedAt
				    LIMIT 1
				)
				WHERE Origin = 'Feed' AND SourceRunId IS NULL AND SourceFeedId IS NOT NULL;
				"""
			);

			migrationBuilder.Sql(
				"""
				UPDATE Watchlist
				SET Origin = 'Orphaned',
				    SourceFeedNameSnapshot = COALESCE(
				        SourceFeedNameSnapshot,
				        (SELECT f.Name FROM Feeds f WHERE f.Id = Watchlist.SourceFeedId),
				        ''
				    ),
				    SourceFeedId = NULL
				WHERE Origin = 'Feed' AND SourceRunId IS NULL;
				"""
			);

			migrationBuilder.CreateIndex(name: "IX_Watchlist_SourceRunId", table: "Watchlist", column: "SourceRunId");

			migrationBuilder.AddForeignKey(
				name: "FK_Watchlist_FeedRuns_SourceRunId",
				table: "Watchlist",
				column: "SourceRunId",
				principalTable: "FeedRuns",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(name: "FK_Watchlist_FeedRuns_SourceRunId", table: "Watchlist");

			migrationBuilder.DropIndex(name: "IX_Watchlist_SourceRunId", table: "Watchlist");
		}
	}
}
