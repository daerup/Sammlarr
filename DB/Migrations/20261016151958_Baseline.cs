using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261016151958_Baseline : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AnimeIds",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Type = table.Column<string>(type: "TEXT", nullable: false),
				AniListId = table.Column<int>(type: "INTEGER", nullable: true),
				MalId = table.Column<int>(type: "INTEGER", nullable: true),
				TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
				TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
				ImdbId = table.Column<string>(type: "TEXT", nullable: true),
				SeriesTvdbId = table.Column<int>(type: "INTEGER", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AnimeIds", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetRoles",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetRoles", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetUsers",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				DisplayName = table.Column<string>(type: "TEXT", nullable: false),
				TagName = table.Column<string>(type: "TEXT", nullable: false),
				AvatarUrl = table.Column<string>(type: "TEXT", nullable: false),
				JoinedAt = table.Column<long>(type: "INTEGER", nullable: true),
				Hue = table.Column<double>(type: "REAL", nullable: true),
				PictureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				PictureHue = table.Column<double>(type: "REAL", nullable: true),
				PictureChroma = table.Column<double>(type: "REAL", nullable: true),
				ConfigResetHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
				PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
				SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
				ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
				PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
				PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
				TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
				LockoutEnd = table.Column<long>(type: "INTEGER", nullable: true),
				LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
				AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetUsers", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "CronTickers",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Expression = table.Column<string>(type: "TEXT", nullable: true),
				Request = table.Column<byte[]>(type: "BLOB", nullable: true),
				Retries = table.Column<int>(type: "INTEGER", nullable: false),
				RetryIntervals = table.Column<string>(type: "TEXT", nullable: true),
				IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
				IsSystemPaused = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
				Function = table.Column<string>(type: "TEXT", nullable: true),
				Description = table.Column<string>(type: "TEXT", nullable: true),
				InitIdentifier = table.Column<string>(type: "TEXT", nullable: true),
				CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
				UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_CronTickers", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "DatasetChecks",
			columns: table => new
			{
				Name = table.Column<string>(type: "TEXT", nullable: false),
				StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
				FinishedAt = table.Column<long>(type: "INTEGER", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				Failure = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DatasetChecks", x => x.Name);
			}
		);

		migrationBuilder.CreateTable(
			name: "DatasetLoads",
			columns: table => new
			{
				Name = table.Column<string>(type: "TEXT", nullable: false),
				LoadedAt = table.Column<long>(type: "INTEGER", nullable: false),
				ETag = table.Column<string>(type: "TEXT", nullable: true),
				Rows = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DatasetLoads", x => x.Name);
			}
		);

		migrationBuilder.CreateTable(
			name: "HousekeepingReports",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
				FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
				BytesBefore = table.Column<long>(type: "INTEGER", nullable: true),
				BytesAfter = table.Column<long>(type: "INTEGER", nullable: true),
				Vacuumed = table.Column<bool>(type: "INTEGER", nullable: true),
				FailedStep = table.Column<string>(type: "TEXT", nullable: true),
				FailureReason = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_HousekeepingReports", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "ImdbRatings",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				Score = table.Column<byte>(type: "INTEGER", nullable: false),
				Votes = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ImdbRatings", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "Media",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				DetailsTrimmedAt = table.Column<long>(type: "INTEGER", nullable: true),
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
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				Sink = table.Column<string>(type: "TEXT", nullable: false),
				TrendingProvider = table.Column<string>(type: "TEXT", nullable: false),
				SearchProvider = table.Column<string>(type: "TEXT", nullable: false),
				AuthProvider = table.Column<string>(type: "TEXT", nullable: false),
				RegistrationMode = table.Column<string>(type: "TEXT", nullable: false),
				OnboardingCompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
				Radarr_Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				Radarr_ExternalUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				Radarr_ApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				Radarr_QualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				Radarr_RootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				Radarr_AnimeQualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				Radarr_AnimeRootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				Radarr_Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
				Radarr_SearchOnAdd = table.Column<bool>(type: "INTEGER", nullable: false),
				Radarr_DeleteFiles = table.Column<bool>(type: "INTEGER", nullable: false),
				Sonarr_Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				Sonarr_ExternalUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				Sonarr_ApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				Sonarr_QualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				Sonarr_RootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				Sonarr_AnimeQualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				Sonarr_AnimeRootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				Sonarr_Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
				Sonarr_SearchOnAdd = table.Column<bool>(type: "INTEGER", nullable: false),
				Sonarr_DeleteFiles = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagPrefix = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
				ArrTagPerUser = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagUserSource = table.Column<string>(type: "TEXT", nullable: false),
				ArrTagPerFeed = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagFeedSource = table.Column<string>(type: "TEXT", nullable: false),
				ArrTagPlainPrefix = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagProtection = table.Column<string>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ProviderConfig", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "RemoteAttempts",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Hour = table.Column<long>(type: "INTEGER", nullable: false),
				Cause = table.Column<string>(type: "TEXT", nullable: false),
				FeedId = table.Column<Guid>(type: "TEXT", nullable: true),
				Attempts = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteAttempts", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "RemoteUsage",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Hour = table.Column<long>(type: "INTEGER", nullable: false),
				Cause = table.Column<string>(type: "TEXT", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				FeedId = table.Column<Guid>(type: "TEXT", nullable: true),
				Calls = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteUsage", x => x.Id);
			}
		);

		migrationBuilder.CreateTable(
			name: "TimeTickers",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Function = table.Column<string>(type: "TEXT", nullable: true),
				Description = table.Column<string>(type: "TEXT", nullable: true),
				InitIdentifier = table.Column<string>(type: "TEXT", nullable: true),
				CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
				UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
				Status = table.Column<string>(type: "TEXT", nullable: false),
				LockHolder = table.Column<string>(type: "TEXT", nullable: true),
				Request = table.Column<byte[]>(type: "BLOB", nullable: true),
				ExecutionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
				LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
				ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
				ExceptionMessage = table.Column<string>(type: "TEXT", nullable: true),
				SkippedReason = table.Column<string>(type: "TEXT", nullable: true),
				ElapsedTime = table.Column<long>(type: "INTEGER", nullable: false),
				Retries = table.Column<int>(type: "INTEGER", nullable: false),
				RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
				RetryIntervals = table.Column<string>(type: "TEXT", nullable: true),
				ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
				RunCondition = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TimeTickers", x => x.Id);
				table.ForeignKey(name: "FK_TimeTickers_TimeTickers_ParentId", column: x => x.ParentId, principalTable: "TimeTickers", principalColumn: "Id");
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetRoleClaims",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
				ClaimType = table.Column<string>(type: "TEXT", nullable: true),
				ClaimValue = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
				table.ForeignKey(
					name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
					column: x => x.RoleId,
					principalTable: "AspNetRoles",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "ApiKeyConfigs",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				ApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ApiKeyConfigs", x => x.Id);
				table.ForeignKey(
					name: "FK_ApiKeyConfigs_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetUserClaims",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				ClaimType = table.Column<string>(type: "TEXT", nullable: true),
				ClaimValue = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
				table.ForeignKey(
					name: "FK_AspNetUserClaims_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetUserLogins",
			columns: table => new
			{
				LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
				ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
				ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
				table.ForeignKey(
					name: "FK_AspNetUserLogins_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetUserRoles",
			columns: table => new { UserId = table.Column<Guid>(type: "TEXT", nullable: false), RoleId = table.Column<Guid>(type: "TEXT", nullable: false) },
			constraints: table =>
			{
				table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
				table.ForeignKey(
					name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
					column: x => x.RoleId,
					principalTable: "AspNetRoles",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_AspNetUserRoles_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "AspNetUserTokens",
			columns: table => new
			{
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", nullable: false),
				Value = table.Column<string>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey(
					"PK_AspNetUserTokens",
					x => new
					{
						x.UserId,
						x.LoginProvider,
						x.Name,
					}
				);
				table.ForeignKey(
					name: "FK_AspNetUserTokens_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "Credentials",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				ClientId = table.Column<string>(type: "TEXT", nullable: false),
				AccessToken = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
				ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Credentials", x => x.Id);
				table.ForeignKey(
					name: "FK_Credentials_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "DeletedFeeds",
			columns: table => new
			{
				FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", nullable: false),
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				DeletedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_DeletedFeeds", x => x.FeedId);
				table.ForeignKey(
					name: "FK_DeletedFeeds_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
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
				Trigger = table.Column<string>(type: "TEXT", nullable: false),
				RatingStrategy = table.Column<string>(type: "TEXT", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Feeds", x => x.Id);
				table.ForeignKey(
					name: "FK_Feeds_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "Jobs",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
				Kind = table.Column<string>(type: "TEXT", nullable: false),
				Key = table.Column<string>(type: "TEXT", nullable: false),
				Request = table.Column<string>(type: "TEXT", nullable: false),
				State = table.Column<string>(type: "TEXT", nullable: false),
				RunAfter = table.Column<long>(type: "INTEGER", nullable: false),
				Deadline = table.Column<long>(type: "INTEGER", nullable: false),
				Attempts = table.Column<int>(type: "INTEGER", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Jobs", x => x.Id);
				table.ForeignKey(
					name: "FK_Jobs_AspNetUsers_OwnerId",
					column: x => x.OwnerId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "NotificationTargets",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
				Channel = table.Column<string>(type: "TEXT", nullable: false),
				Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
				Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				SigningSecret = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				AppToken = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				UserKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_NotificationTargets", x => x.Id);
				table.ForeignKey(
					name: "FK_NotificationTargets_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "RatingDefaults",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Strategy = table.Column<string>(type: "TEXT", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RatingDefaults", x => x.Id);
				table.ForeignKey(
					name: "FK_RatingDefaults_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "CronTickerOccurrences",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Status = table.Column<string>(type: "TEXT", nullable: false),
				LockHolder = table.Column<string>(type: "TEXT", nullable: true),
				ExecutionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
				CronTickerId = table.Column<Guid>(type: "TEXT", nullable: false),
				LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
				ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
				ExceptionMessage = table.Column<string>(type: "TEXT", nullable: true),
				SkippedReason = table.Column<string>(type: "TEXT", nullable: true),
				ElapsedTime = table.Column<long>(type: "INTEGER", nullable: false),
				RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
				CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
				UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_CronTickerOccurrences", x => x.Id);
				table.ForeignKey(
					name: "FK_CronTickerOccurrences_CronTickers_CronTickerId",
					column: x => x.CronTickerId,
					principalTable: "CronTickers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "HousekeepingReportSteps",
			columns: table => new
			{
				ReportId = table.Column<int>(type: "INTEGER", nullable: false),
				Step = table.Column<string>(type: "TEXT", nullable: false),
				Removed = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_HousekeepingReportSteps", x => new { x.ReportId, x.Step });
				table.ForeignKey(
					name: "FK_HousekeepingReportSteps_HousekeepingReports_ReportId",
					column: x => x.ReportId,
					principalTable: "HousekeepingReports",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "Blocklist",
			columns: table => new
			{
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Blocklist", x => new { x.UserId, x.MediaId });
				table.ForeignKey(
					name: "FK_Blocklist_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_Blocklist_Media_MediaId",
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
			name: "MediaDetails",
			columns: table => new
			{
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				RefreshedAt = table.Column<long>(type: "INTEGER", nullable: false),
				AskedAt = table.Column<long>(type: "INTEGER", nullable: false),
				Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
				Tagline = table.Column<string>(type: "TEXT", nullable: true),
				Summary = table.Column<string>(type: "TEXT", nullable: true),
				PosterUrl = table.Column<string>(type: "TEXT", nullable: true),
				BackdropUrl = table.Column<string>(type: "TEXT", nullable: true),
				ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
				Genres = table.Column<string>(type: "TEXT", nullable: false),
				MediaType = table.Column<string>(type: "TEXT", nullable: false),
				Runtime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
				Seasons = table.Column<int>(type: "INTEGER", nullable: true),
				Episodes = table.Column<int>(type: "INTEGER", nullable: true),
				RuntimePerEpisode = table.Column<TimeSpan>(type: "TEXT", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_MediaDetails", x => x.MediaId);
				table.ForeignKey(
					name: "FK_MediaDetails_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "WantlistOutbox",
			columns: table => new
			{
				Sink = table.Column<string>(type: "TEXT", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Action = table.Column<string>(type: "TEXT", nullable: false),
				Status = table.Column<string>(type: "TEXT", nullable: false),
				Attempts = table.Column<int>(type: "INTEGER", nullable: false),
				LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
				AddedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey(
					"PK_WantlistOutbox",
					x => new
					{
						x.UserId,
						x.MediaId,
						x.Sink,
					}
				);
				table.ForeignKey(
					name: "FK_WantlistOutbox_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_WantlistOutbox_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "RemoteAttemptBuckets",
			columns: table => new
			{
				UpToMs = table.Column<int>(type: "INTEGER", nullable: false),
				AttemptId = table.Column<long>(type: "INTEGER", nullable: false),
				Count = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RemoteAttemptBuckets", x => new { x.AttemptId, x.UpToMs });
				table.ForeignKey(
					name: "FK_RemoteAttemptBuckets_RemoteAttempts_AttemptId",
					column: x => x.AttemptId,
					principalTable: "RemoteAttempts",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "FeedAbsences",
			columns: table => new
			{
				FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				MissedRuns = table.Column<int>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedAbsences", x => new { x.FeedId, x.MediaId });
				table.ForeignKey(
					name: "FK_FeedAbsences_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedAbsences_Feeds_FeedId",
					column: x => x.FeedId,
					principalTable: "Feeds",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedAbsences_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "FeedRuns",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedId = table.Column<Guid>(type: "TEXT", nullable: false),
				StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
				DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
				Strategy = table.Column<string>(type: "TEXT", nullable: false),
				Trigger = table.Column<string>(type: "TEXT", nullable: false),
				Conclusiveness = table.Column<string>(type: "TEXT", nullable: false),
				Total = table.Column<int>(type: "INTEGER", nullable: false),
				Added = table.Column<int>(type: "INTEGER", nullable: false),
				Kept = table.Column<int>(type: "INTEGER", nullable: false),
				Bypassed = table.Column<int>(type: "INTEGER", nullable: false),
				Filtered = table.Column<int>(type: "INTEGER", nullable: false),
				Skipped = table.Column<int>(type: "INTEGER", nullable: false),
				UnmatchedNotFound = table.Column<int>(type: "INTEGER", nullable: false),
				UnmatchedCouldNotLookUp = table.Column<int>(type: "INTEGER", nullable: false),
				UnmatchedConflict = table.Column<int>(type: "INTEGER", nullable: false),
				Removed = table.Column<int>(type: "INTEGER", nullable: false),
				Known = table.Column<int>(type: "INTEGER", nullable: false),
				NotFoundEarlier = table.Column<int>(type: "INTEGER", nullable: false),
				SourceFailure = table.Column<string>(type: "TEXT", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRuns", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRuns_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRuns_Feeds_FeedId",
					column: x => x.FeedId,
					principalTable: "Feeds",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "NotificationDeliveryOutcomes",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				EventId = table.Column<Guid>(type: "TEXT", nullable: false),
				TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
				EventType = table.Column<string>(type: "TEXT", nullable: false),
				Kind = table.Column<string>(type: "TEXT", nullable: false),
				Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_NotificationDeliveryOutcomes", x => x.Id);
				table.ForeignKey(
					name: "FK_NotificationDeliveryOutcomes_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_NotificationDeliveryOutcomes_NotificationTargets_TargetId",
					column: x => x.TargetId,
					principalTable: "NotificationTargets",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "NotificationSubscriptions",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
				EventTypes = table.Column<string>(type: "TEXT", nullable: false),
				BodyTemplate = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
				TitleTemplate = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				MessageTemplate = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
				Level = table.Column<string>(type: "TEXT", nullable: true),
				Priority = table.Column<string>(type: "TEXT", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
				table.ForeignKey(
					name: "FK_NotificationSubscriptions_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_NotificationSubscriptions_NotificationTargets_TargetId",
					column: x => x.TargetId,
					principalTable: "NotificationTargets",
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

		migrationBuilder.CreateTable(
			name: "MediaPageLinks",
			columns: table => new
			{
				Site = table.Column<string>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Path = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_MediaPageLinks", x => new { x.MediaId, x.Site });
				table.ForeignKey(
					name: "FK_MediaPageLinks_MediaDetails_MediaId",
					column: x => x.MediaId,
					principalTable: "MediaDetails",
					principalColumn: "MediaId",
					onDelete: ReferentialAction.Cascade
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
					name: "FK_MediaRatings_MediaDetails_MediaId",
					column: x => x.MediaId,
					principalTable: "MediaDetails",
					principalColumn: "MediaId",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "ProviderAnswers",
			columns: table => new
			{
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Unfilled = table.Column<string>(type: "TEXT", nullable: false),
				AnsweredAt = table.Column<long>(type: "INTEGER", nullable: false),
				Failures = table.Column<int>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ProviderAnswers", x => new { x.MediaId, x.Provider });
				table.ForeignKey(
					name: "FK_ProviderAnswers_MediaDetails_MediaId",
					column: x => x.MediaId,
					principalTable: "MediaDetails",
					principalColumn: "MediaId",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "FeedRunCalls",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				Outcome = table.Column<string>(type: "TEXT", nullable: false),
				Calls = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRunCalls", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRunCalls_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunCalls_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "FeedRunItems",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				State = table.Column<string>(type: "TEXT", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRunItems", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRunItems_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunItems_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "FeedRunTimes",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				FeedRunId = table.Column<Guid>(type: "TEXT", nullable: false),
				Service = table.Column<string>(type: "TEXT", nullable: false),
				UpToMs = table.Column<int>(type: "INTEGER", nullable: false),
				Attempts = table.Column<long>(type: "INTEGER", nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRunTimes", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRunTimes_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunTimes_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "Wantlist",
			columns: table => new
			{
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Origin = table.Column<string>(type: "TEXT", nullable: false),
				Sink = table.Column<string>(type: "TEXT", nullable: true),
				ManualSource = table.Column<string>(type: "TEXT", nullable: true),
				SourceFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
				SourceFeedNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
				SourceRunId = table.Column<Guid>(type: "TEXT", nullable: true),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Wantlist", x => new { x.UserId, x.MediaId });
				table.ForeignKey(
					name: "FK_Wantlist_AspNetUsers_UserId",
					column: x => x.UserId,
					principalTable: "AspNetUsers",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_Wantlist_FeedRuns_SourceRunId",
					column: x => x.SourceRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict
				);
				table.ForeignKey(
					name: "FK_Wantlist_Feeds_SourceFeedId",
					column: x => x.SourceFeedId,
					principalTable: "Feeds",
					principalColumn: "Id",
					onDelete: ReferentialAction.SetNull
				);
				table.ForeignKey(
					name: "FK_Wantlist_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_AniListId", table: "AnimeIds", column: "AniListId");

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_ImdbId_Type", table: "AnimeIds", columns: new[] { "ImdbId", "Type" });

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_MalId", table: "AnimeIds", column: "MalId");

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_TmdbId_Type", table: "AnimeIds", columns: new[] { "TmdbId", "Type" });

		migrationBuilder.CreateIndex(name: "IX_AnimeIds_TvdbId", table: "AnimeIds", column: "TvdbId");

		migrationBuilder.CreateIndex(name: "IX_ApiKeyConfigs_UserId_Provider", table: "ApiKeyConfigs", columns: new[] { "UserId", "Provider" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_AspNetRoleClaims_RoleId", table: "AspNetRoleClaims", column: "RoleId");

		migrationBuilder.CreateIndex(name: "RoleNameIndex", table: "AspNetRoles", column: "NormalizedName", unique: true);

		migrationBuilder.CreateIndex(name: "IX_AspNetUserClaims_UserId", table: "AspNetUserClaims", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_AspNetUserLogins_UserId", table: "AspNetUserLogins", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_AspNetUserRoles_RoleId", table: "AspNetUserRoles", column: "RoleId");

		migrationBuilder.CreateIndex(name: "EmailIndex", table: "AspNetUsers", column: "NormalizedEmail");

		migrationBuilder.CreateIndex(name: "UserNameIndex", table: "AspNetUsers", column: "NormalizedUserName", unique: true);

		migrationBuilder.CreateIndex(name: "IX_Blocklist_MediaId", table: "Blocklist", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_Credentials_UserId_Provider", table: "Credentials", columns: new[] { "UserId", "Provider" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_CronTickerOccurrence_CronTickerId", table: "CronTickerOccurrences", column: "CronTickerId");

		migrationBuilder.CreateIndex(name: "IX_CronTickerOccurrence_ExecutionTime", table: "CronTickerOccurrences", column: "ExecutionTime");

		migrationBuilder.CreateIndex(
			name: "IX_CronTickerOccurrence_Status_ExecutionTime",
			table: "CronTickerOccurrences",
			columns: new[] { "Status", "ExecutionTime" }
		);

		migrationBuilder.CreateIndex(
			name: "UQ_CronTickerId_ExecutionTime",
			table: "CronTickerOccurrences",
			columns: new[] { "CronTickerId", "ExecutionTime" },
			unique: true
		);

		migrationBuilder.CreateIndex(name: "IX_CronTickers_Expression", table: "CronTickers", column: "Expression");

		migrationBuilder.CreateIndex(name: "IX_Function_Expression", table: "CronTickers", columns: new[] { "Function", "Expression" });

		migrationBuilder.CreateIndex(name: "IX_DeletedFeeds_DeletedAt", table: "DeletedFeeds", column: "DeletedAt");

		migrationBuilder.CreateIndex(name: "IX_DeletedFeeds_UserId", table: "DeletedFeeds", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_ExternalMediaIds_MediaId_Source", table: "ExternalMediaIds", columns: new[] { "MediaId", "Source" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_FeedAbsences_MediaId", table: "FeedAbsences", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_FeedAbsences_UserId", table: "FeedAbsences", column: "UserId");

		migrationBuilder.CreateIndex(
			name: "IX_FeedRunCalls_FeedRunId_Service_Outcome",
			table: "FeedRunCalls",
			columns: new[] { "FeedRunId", "Service", "Outcome" },
			unique: true
		);

		migrationBuilder.CreateIndex(name: "IX_FeedRunCalls_UserId", table: "FeedRunCalls", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_FeedRunItems_FeedRunId_MediaId", table: "FeedRunItems", columns: new[] { "FeedRunId", "MediaId" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_FeedRunItems_UserId", table: "FeedRunItems", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_FeedRuns_FeedId_StartedAt", table: "FeedRuns", columns: new[] { "FeedId", "StartedAt" });

		migrationBuilder.CreateIndex(name: "IX_FeedRuns_UserId", table: "FeedRuns", column: "UserId");

		migrationBuilder.CreateIndex(
			name: "IX_FeedRunTimes_FeedRunId_Service_UpToMs",
			table: "FeedRunTimes",
			columns: new[] { "FeedRunId", "Service", "UpToMs" },
			unique: true
		);

		migrationBuilder.CreateIndex(name: "IX_FeedRunTimes_UserId", table: "FeedRunTimes", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_Feeds_UserId", table: "Feeds", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_IdentityConflicts_HeldId_OtherId", table: "IdentityConflicts", columns: new[] { "HeldId", "OtherId" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_IdentityConflicts_OtherId", table: "IdentityConflicts", column: "OtherId");

		migrationBuilder.CreateIndex(
			name: "IX_Jobs_Kind_Key_OwnerId",
			table: "Jobs",
			columns: new[] { "Kind", "Key", "OwnerId" },
			unique: true,
			filter: "\"State\" IN ('Queued', 'Waiting')"
		);

		migrationBuilder.CreateIndex(name: "IX_Jobs_OwnerId", table: "Jobs", column: "OwnerId");

		migrationBuilder.CreateIndex(name: "IX_Jobs_State_RunAfter", table: "Jobs", columns: new[] { "State", "RunAfter" });

		migrationBuilder.CreateIndex(name: "IX_MediaRatings_Source", table: "MediaRatings", column: "Source");

		migrationBuilder.CreateIndex(
			name: "IX_NotificationDeliveryOutcomes_EventId_TargetId",
			table: "NotificationDeliveryOutcomes",
			columns: new[] { "EventId", "TargetId" },
			unique: true
		);

		migrationBuilder.CreateIndex(
			name: "IX_NotificationDeliveryOutcomes_TargetId_UpdatedAt",
			table: "NotificationDeliveryOutcomes",
			columns: new[] { "TargetId", "UpdatedAt" }
		);

		migrationBuilder.CreateIndex(name: "IX_NotificationDeliveryOutcomes_UserId", table: "NotificationDeliveryOutcomes", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_NotificationSubscriptions_TargetId", table: "NotificationSubscriptions", column: "TargetId");

		migrationBuilder.CreateIndex(name: "IX_NotificationSubscriptions_UserId", table: "NotificationSubscriptions", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_NotificationTargets_UserId_Channel", table: "NotificationTargets", columns: new[] { "UserId", "Channel" });

		migrationBuilder.CreateIndex(name: "IX_RatingDefaults_UserId", table: "RatingDefaults", column: "UserId", unique: true);

		migrationBuilder.CreateIndex(name: "IX_RemoteAttempts_Hour", table: "RemoteAttempts", column: "Hour");

		migrationBuilder.CreateIndex(name: "IX_RemoteUsage_Hour", table: "RemoteUsage", column: "Hour");

		migrationBuilder.CreateIndex(name: "IX_TimeTicker_ExecutionTime", table: "TimeTickers", column: "ExecutionTime");

		migrationBuilder.CreateIndex(name: "IX_TimeTicker_Status_ExecutionTime", table: "TimeTickers", columns: new[] { "Status", "ExecutionTime" });

		migrationBuilder.CreateIndex(name: "IX_TimeTickers_ParentId", table: "TimeTickers", column: "ParentId");

		migrationBuilder.CreateIndex(name: "IX_Wantlist_MediaId", table: "Wantlist", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_Wantlist_SourceFeedId", table: "Wantlist", column: "SourceFeedId");

		migrationBuilder.CreateIndex(name: "IX_Wantlist_SourceRunId", table: "Wantlist", column: "SourceRunId");

		migrationBuilder.CreateIndex(name: "IX_WantlistOutbox_Action", table: "WantlistOutbox", column: "Action");

		migrationBuilder.CreateIndex(name: "IX_WantlistOutbox_MediaId", table: "WantlistOutbox", column: "MediaId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "AnimeIds");

		migrationBuilder.DropTable(name: "ApiKeyConfigs");

		migrationBuilder.DropTable(name: "AspNetRoleClaims");

		migrationBuilder.DropTable(name: "AspNetUserClaims");

		migrationBuilder.DropTable(name: "AspNetUserLogins");

		migrationBuilder.DropTable(name: "AspNetUserRoles");

		migrationBuilder.DropTable(name: "AspNetUserTokens");

		migrationBuilder.DropTable(name: "Blocklist");

		migrationBuilder.DropTable(name: "Credentials");

		migrationBuilder.DropTable(name: "CronTickerOccurrences");

		migrationBuilder.DropTable(name: "DatasetChecks");

		migrationBuilder.DropTable(name: "DatasetLoads");

		migrationBuilder.DropTable(name: "DeletedFeeds");

		migrationBuilder.DropTable(name: "ExternalMediaIds");

		migrationBuilder.DropTable(name: "FeedAbsences");

		migrationBuilder.DropTable(name: "FeedRunCalls");

		migrationBuilder.DropTable(name: "FeedRunItems");

		migrationBuilder.DropTable(name: "FeedRunTimes");

		migrationBuilder.DropTable(name: "HousekeepingReportSteps");

		migrationBuilder.DropTable(name: "IdentityConflictSources");

		migrationBuilder.DropTable(name: "ImdbRatings");

		migrationBuilder.DropTable(name: "Jobs");

		migrationBuilder.DropTable(name: "MediaPageLinks");

		migrationBuilder.DropTable(name: "MediaRatings");

		migrationBuilder.DropTable(name: "NotificationDeliveryOutcomes");

		migrationBuilder.DropTable(name: "NotificationSubscriptions");

		migrationBuilder.DropTable(name: "ProviderAnswers");

		migrationBuilder.DropTable(name: "ProviderConfig");

		migrationBuilder.DropTable(name: "RatingDefaults");

		migrationBuilder.DropTable(name: "RemoteAttemptBuckets");

		migrationBuilder.DropTable(name: "RemoteUsage");

		migrationBuilder.DropTable(name: "TimeTickers");

		migrationBuilder.DropTable(name: "Wantlist");

		migrationBuilder.DropTable(name: "WantlistOutbox");

		migrationBuilder.DropTable(name: "AspNetRoles");

		migrationBuilder.DropTable(name: "CronTickers");

		migrationBuilder.DropTable(name: "HousekeepingReports");

		migrationBuilder.DropTable(name: "IdentityConflicts");

		migrationBuilder.DropTable(name: "NotificationTargets");

		migrationBuilder.DropTable(name: "MediaDetails");

		migrationBuilder.DropTable(name: "RemoteAttempts");

		migrationBuilder.DropTable(name: "FeedRuns");

		migrationBuilder.DropTable(name: "Media");

		migrationBuilder.DropTable(name: "Feeds");

		migrationBuilder.DropTable(name: "AspNetUsers");
	}
}
