using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261004071512_Baseline : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
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
				Id = table.Column<int>(type: "INTEGER", nullable: false),
				Sink = table.Column<string>(type: "TEXT", nullable: false),
				TrendingProvider = table.Column<string>(type: "TEXT", nullable: false),
				SearchProvider = table.Column<string>(type: "TEXT", nullable: false),
				AuthProvider = table.Column<string>(type: "TEXT", nullable: false),
				RegistrationMode = table.Column<string>(type: "TEXT", nullable: false),
				OnboardingCompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
				RadarrUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				RadarrApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				RadarrQualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				RadarrRootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				RadarrMonitored = table.Column<bool>(type: "INTEGER", nullable: false),
				RadarrSearchOnAdd = table.Column<bool>(type: "INTEGER", nullable: false),
				SonarrUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
				SonarrApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
				SonarrQualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
				SonarrRootFolderPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
				SonarrMonitored = table.Column<bool>(type: "INTEGER", nullable: false),
				SonarrSearchOnAdd = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagPrefix = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
				ArrTagPerUser = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagUserSource = table.Column<string>(type: "TEXT", nullable: false),
				ArrTagPerFeed = table.Column<bool>(type: "INTEGER", nullable: false),
				ArrTagFeedSource = table.Column<string>(type: "TEXT", nullable: false),
				ArrTagPlainPrefix = table.Column<bool>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ProviderConfig", x => x.Id);
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
			name: "Users",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Provider = table.Column<string>(type: "TEXT", nullable: false),
				ExternalUserId = table.Column<string>(type: "TEXT", nullable: false),
				DisplayName = table.Column<string>(type: "TEXT", nullable: false),
				AvatarUrl = table.Column<string>(type: "TEXT", nullable: false),
				Role = table.Column<string>(type: "TEXT", nullable: false),
				JoinedAt = table.Column<long>(type: "INTEGER", nullable: false),
				Hue = table.Column<double>(type: "REAL", nullable: false),
				PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
				FailedSignIns = table.Column<int>(type: "INTEGER", nullable: false),
				LockedUntil = table.Column<long>(type: "INTEGER", nullable: true),
				SessionsRevokedAt = table.Column<long>(type: "INTEGER", nullable: true),
				KeyPromptedAt = table.Column<long>(type: "INTEGER", nullable: true),
				ConfigResetHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Users", x => x.Id);
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
			name: "MediaDetails",
			columns: table => new
			{
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				FetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
				Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
				Tagline = table.Column<string>(type: "TEXT", nullable: true),
				Summary = table.Column<string>(type: "TEXT", nullable: true),
				PosterUrl = table.Column<string>(type: "TEXT", nullable: true),
				BackdropUrl = table.Column<string>(type: "TEXT", nullable: true),
				ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
				Genres = table.Column<string>(type: "TEXT", nullable: false),
				MediaType = table.Column<string>(type: "TEXT", nullable: false),
				ConfirmedAbsent = table.Column<string>(type: "TEXT", nullable: false),
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
			name: "AccountLinks",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				Kind = table.Column<string>(type: "TEXT", nullable: false),
				Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				UserId = table.Column<Guid>(type: "TEXT", nullable: true),
				TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
				IssuedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
				ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AccountLinks", x => x.Id);
				table.ForeignKey(
					name: "FK_AccountLinks_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_ApiKeyConfigs_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_Blocklist_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict
				);
				table.ForeignKey(
					name: "FK_Blocklist_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_Credentials_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_Feeds_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_Jobs_Users_OwnerId",
					column: x => x.OwnerId,
					principalTable: "Users",
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
					name: "FK_NotificationTargets_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_RatingDefaults_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateTable(
			name: "WantlistOutbox",
			columns: table => new
			{
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
				Action = table.Column<string>(type: "TEXT", nullable: false),
				Status = table.Column<string>(type: "TEXT", nullable: false),
				Attempts = table.Column<int>(type: "INTEGER", nullable: false),
				AddedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_WantlistOutbox", x => new { x.UserId, x.MediaId });
				table.ForeignKey(
					name: "FK_WantlistOutbox_Media_MediaId",
					column: x => x.MediaId,
					principalTable: "Media",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict
				);
				table.ForeignKey(
					name: "FK_WantlistOutbox_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
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
				RottenTomatoesPath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
				RottenTomatoesAskedAt = table.Column<long>(type: "INTEGER", nullable: true),
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
				Unmatched = table.Column<int>(type: "INTEGER", nullable: false),
				Removed = table.Column<int>(type: "INTEGER", nullable: false),
				SourceFailure = table.Column<string>(type: "TEXT", nullable: true),
				UserId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
				UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_FeedRuns", x => x.Id);
				table.ForeignKey(
					name: "FK_FeedRuns_Feeds_FeedId",
					column: x => x.FeedId,
					principalTable: "Feeds",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRuns_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_NotificationDeliveryOutcomes_NotificationTargets_TargetId",
					column: x => x.TargetId,
					principalTable: "NotificationTargets",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_NotificationDeliveryOutcomes_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_NotificationSubscriptions_NotificationTargets_TargetId",
					column: x => x.TargetId,
					principalTable: "NotificationTargets",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_NotificationSubscriptions_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
					name: "FK_FeedRunItems_FeedRuns_FeedRunId",
					column: x => x.FeedRunId,
					principalTable: "FeedRuns",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
				table.ForeignKey(
					name: "FK_FeedRunItems_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
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
				table.ForeignKey(
					name: "FK_Wantlist_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade
				);
			}
		);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_Kind_Username", table: "AccountLinks", columns: new[] { "Kind", "Username" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_TokenHash", table: "AccountLinks", column: "TokenHash", unique: true);

		migrationBuilder.CreateIndex(name: "IX_AccountLinks_UserId", table: "AccountLinks", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_ApiKeyConfigs_UserId_Provider", table: "ApiKeyConfigs", columns: new[] { "UserId", "Provider" }, unique: true);

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

		migrationBuilder.CreateIndex(name: "IX_ExternalMediaIds_MediaId", table: "ExternalMediaIds", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_FeedRunItems_FeedRunId_MediaId", table: "FeedRunItems", columns: new[] { "FeedRunId", "MediaId" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_FeedRunItems_UserId", table: "FeedRunItems", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_FeedRuns_FeedId_StartedAt", table: "FeedRuns", columns: new[] { "FeedId", "StartedAt" });

		migrationBuilder.CreateIndex(name: "IX_FeedRuns_UserId", table: "FeedRuns", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_Feeds_UserId", table: "Feeds", column: "UserId");

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

		migrationBuilder.CreateIndex(name: "IX_TimeTicker_ExecutionTime", table: "TimeTickers", column: "ExecutionTime");

		migrationBuilder.CreateIndex(name: "IX_TimeTicker_Status_ExecutionTime", table: "TimeTickers", columns: new[] { "Status", "ExecutionTime" });

		migrationBuilder.CreateIndex(name: "IX_TimeTickers_ParentId", table: "TimeTickers", column: "ParentId");

		migrationBuilder.CreateIndex(name: "IX_Users_Provider_ExternalUserId", table: "Users", columns: new[] { "Provider", "ExternalUserId" }, unique: true);

		migrationBuilder.CreateIndex(name: "IX_Wantlist_MediaId", table: "Wantlist", column: "MediaId");

		migrationBuilder.CreateIndex(name: "IX_Wantlist_SourceFeedId", table: "Wantlist", column: "SourceFeedId");

		migrationBuilder.CreateIndex(name: "IX_Wantlist_SourceRunId", table: "Wantlist", column: "SourceRunId");

		migrationBuilder.CreateIndex(name: "IX_WantlistOutbox_Action", table: "WantlistOutbox", column: "Action");

		migrationBuilder.CreateIndex(name: "IX_WantlistOutbox_MediaId", table: "WantlistOutbox", column: "MediaId");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "AccountLinks");

		migrationBuilder.DropTable(name: "ApiKeyConfigs");

		migrationBuilder.DropTable(name: "Blocklist");

		migrationBuilder.DropTable(name: "Credentials");

		migrationBuilder.DropTable(name: "CronTickerOccurrences");

		migrationBuilder.DropTable(name: "ExternalMediaIds");

		migrationBuilder.DropTable(name: "FeedRunItems");

		migrationBuilder.DropTable(name: "Jobs");

		migrationBuilder.DropTable(name: "MediaRatings");

		migrationBuilder.DropTable(name: "NotificationDeliveryOutcomes");

		migrationBuilder.DropTable(name: "NotificationSubscriptions");

		migrationBuilder.DropTable(name: "ProviderConfig");

		migrationBuilder.DropTable(name: "RatingDefaults");

		migrationBuilder.DropTable(name: "TimeTickers");

		migrationBuilder.DropTable(name: "Wantlist");

		migrationBuilder.DropTable(name: "WantlistOutbox");

		migrationBuilder.DropTable(name: "CronTickers");

		migrationBuilder.DropTable(name: "MediaDetails");

		migrationBuilder.DropTable(name: "NotificationTargets");

		migrationBuilder.DropTable(name: "FeedRuns");

		migrationBuilder.DropTable(name: "Media");

		migrationBuilder.DropTable(name: "Feeds");

		migrationBuilder.DropTable(name: "Users");
	}
}
