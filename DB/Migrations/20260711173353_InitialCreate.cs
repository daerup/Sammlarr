using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class InitialCreate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "ApiKeyConfigs",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					Provider = table.Column<string>(type: "TEXT", nullable: false),
					ApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ApiKeyConfigs", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "Credentials",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					WatchlistProvider = table.Column<string>(type: "TEXT", nullable: false),
					ClientId = table.Column<string>(type: "TEXT", nullable: false),
					AccessToken = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
					ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Credentials", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "Feeds",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
					Name = table.Column<string>(type: "TEXT", nullable: false),
					Provider = table.Column<string>(type: "TEXT", nullable: false),
					Strategy = table.Column<string>(type: "TEXT", nullable: false),
					Identifier = table.Column<string>(type: "TEXT", nullable: false),
					Filter = table.Column<string>(type: "TEXT", nullable: true),
					Sort = table.Column<string>(type: "TEXT", nullable: true),
					Limit = table.Column<string>(type: "TEXT", nullable: true),
					SyncMode = table.Column<string>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Feeds", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "Media",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Media", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "ProviderConfig",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "TEXT", nullable: false),
					WatchlistProvider = table.Column<string>(type: "TEXT", nullable: false),
					TrendingProvider = table.Column<string>(type: "TEXT", nullable: false),
					SearchProvider = table.Column<string>(type: "TEXT", nullable: false),
					RatingProvider = table.Column<string>(type: "TEXT", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ProviderConfig", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "Blacklist",
				columns: table => new
				{
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Blacklist", x => x.MediaId);
					table.ForeignKey(
						name: "FK_Blacklist_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "ExternalMediaIds",
				columns: table => new
				{
					Source = table.Column<string>(type: "TEXT", nullable: false),
					MediaType = table.Column<string>(type: "TEXT", nullable: false),
					Id = table.Column<string>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey(
						"PK_ExternalMediaIds",
						x => new
						{
							x.Source,
							x.MediaType,
							x.Id,
						}
					);
					table.ForeignKey(
						name: "FK_ExternalMediaIds_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "MediaMetadata",
				columns: table => new
				{
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					LastFetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
					Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
					Tagline = table.Column<string>(type: "TEXT", nullable: false),
					Summary = table.Column<string>(type: "TEXT", nullable: false),
					PosterUrl = table.Column<string>(type: "TEXT", nullable: false),
					BackdropUrl = table.Column<string>(type: "TEXT", nullable: false),
					ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
					Genres = table.Column<string>(type: "TEXT", nullable: false),
					MediaType = table.Column<string>(type: "TEXT", nullable: false),
					Runtime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
					Seasons = table.Column<int>(type: "INTEGER", nullable: true),
					Episodes = table.Column<int>(type: "INTEGER", nullable: true),
					RuntimePerEpisode = table.Column<TimeSpan>(type: "TEXT", nullable: true),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_MediaMetadata", x => x.MediaId);
					table.ForeignKey(
						name: "FK_MediaMetadata_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "Watchlist",
				columns: table => new
				{
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					Origin = table.Column<string>(type: "TEXT", nullable: false),
					SourceFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
					SourceFeedNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
					CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
					UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Watchlist", x => x.MediaId);
					table.ForeignKey(
						name: "FK_Watchlist_Feeds_SourceFeedId",
						column: x => x.SourceFeedId,
						principalTable: "Feeds",
						principalColumn: "Id",
						onDelete: ReferentialAction.SetNull
					);
					table.ForeignKey(
						name: "FK_Watchlist_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "WatchlistOutbox",
				columns: table => new
				{
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					Kind = table.Column<string>(type: "TEXT", nullable: false),
					Attempts = table.Column<int>(type: "INTEGER", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_WatchlistOutbox", x => x.MediaId);
					table.ForeignKey(
						name: "FK_WatchlistOutbox_Media_MediaId",
						column: x => x.MediaId,
						principalTable: "Media",
						principalColumn: "Id",
						onDelete: ReferentialAction.Restrict
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "MediaRatings",
				columns: table => new
				{
					Source = table.Column<string>(type: "TEXT", nullable: false),
					MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
					NormalizedValue = table.Column<float>(type: "REAL", nullable: false),
					VoteCount = table.Column<int>(type: "INTEGER", nullable: true),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_MediaRatings", x => new { x.MediaId, x.Source });
					table.ForeignKey(
						name: "FK_MediaRatings_MediaMetadata_MediaId",
						column: x => x.MediaId,
						principalTable: "MediaMetadata",
						principalColumn: "MediaId",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.InsertData(
				table: "ProviderConfig",
				columns: new[] { "Id", "RatingProvider", "SearchProvider", "TrendingProvider", "WatchlistProvider" },
				values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Tmdb", "Plex", "Tmdb", "Plex" }
			);

			migrationBuilder.CreateIndex(name: "IX_ApiKeyConfigs_Provider", table: "ApiKeyConfigs", column: "Provider", unique: true);

			migrationBuilder.CreateIndex(name: "IX_Credentials_WatchlistProvider", table: "Credentials", column: "WatchlistProvider", unique: true);

			migrationBuilder.CreateIndex(name: "IX_ExternalMediaIds_MediaId", table: "ExternalMediaIds", column: "MediaId");

			migrationBuilder.CreateIndex(name: "IX_MediaRatings_Source", table: "MediaRatings", column: "Source");

			migrationBuilder.CreateIndex(name: "IX_Watchlist_SourceFeedId", table: "Watchlist", column: "SourceFeedId");

			migrationBuilder.CreateIndex(name: "IX_WatchlistOutbox_Kind", table: "WatchlistOutbox", column: "Kind");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "ApiKeyConfigs");

			migrationBuilder.DropTable(name: "Blacklist");

			migrationBuilder.DropTable(name: "Credentials");

			migrationBuilder.DropTable(name: "ExternalMediaIds");

			migrationBuilder.DropTable(name: "MediaRatings");

			migrationBuilder.DropTable(name: "ProviderConfig");

			migrationBuilder.DropTable(name: "Watchlist");

			migrationBuilder.DropTable(name: "WatchlistOutbox");

			migrationBuilder.DropTable(name: "MediaMetadata");

			migrationBuilder.DropTable(name: "Feeds");

			migrationBuilder.DropTable(name: "Media");
		}
	}
}
