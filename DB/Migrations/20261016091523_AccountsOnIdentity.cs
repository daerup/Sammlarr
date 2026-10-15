using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations;

/// <inheritdoc />
public partial class _20261016091523_AccountsOnIdentity : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey(name: "FK_AccountLinks_Users_UserId", table: "AccountLinks");

		migrationBuilder.DropForeignKey(name: "FK_ApiKeyConfigs_Users_UserId", table: "ApiKeyConfigs");

		migrationBuilder.DropForeignKey(name: "FK_Blocklist_Users_UserId", table: "Blocklist");

		migrationBuilder.DropForeignKey(name: "FK_Credentials_Users_UserId", table: "Credentials");

		migrationBuilder.DropForeignKey(name: "FK_DeletedFeeds_Users_UserId", table: "DeletedFeeds");

		migrationBuilder.DropForeignKey(name: "FK_FeedAbsences_Users_UserId", table: "FeedAbsences");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunCalls_Users_UserId", table: "FeedRunCalls");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunItems_Users_UserId", table: "FeedRunItems");

		migrationBuilder.DropForeignKey(name: "FK_FeedRuns_Users_UserId", table: "FeedRuns");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunTimes_Users_UserId", table: "FeedRunTimes");

		migrationBuilder.DropForeignKey(name: "FK_Feeds_Users_UserId", table: "Feeds");

		migrationBuilder.DropForeignKey(name: "FK_Jobs_Users_OwnerId", table: "Jobs");

		migrationBuilder.DropForeignKey(name: "FK_NotificationDeliveryOutcomes_Users_UserId", table: "NotificationDeliveryOutcomes");

		migrationBuilder.DropForeignKey(name: "FK_NotificationSubscriptions_Users_UserId", table: "NotificationSubscriptions");

		migrationBuilder.DropForeignKey(name: "FK_NotificationTargets_Users_UserId", table: "NotificationTargets");

		migrationBuilder.DropForeignKey(name: "FK_RatingDefaults_Users_UserId", table: "RatingDefaults");

		migrationBuilder.DropForeignKey(name: "FK_Wantlist_Users_UserId", table: "Wantlist");

		migrationBuilder.DropForeignKey(name: "FK_WantlistOutbox_Users_UserId", table: "WantlistOutbox");

		migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");

		migrationBuilder.DropIndex(name: "IX_Users_Provider_ExternalUserId", table: "Users");

		migrationBuilder.DropColumn(name: "ExternalUserId", table: "Users");

		migrationBuilder.DropColumn(name: "LockedUntil", table: "Users");

		migrationBuilder.DropColumn(name: "Provider", table: "Users");

		migrationBuilder.DropColumn(name: "Role", table: "Users");

		migrationBuilder.RenameTable(name: "Users", newName: "AspNetUsers");

		migrationBuilder.DropColumn(name: "SessionsRevokedAt", table: "AspNetUsers");

		migrationBuilder.AddColumn<long>(name: "LockoutEnd", table: "AspNetUsers", type: "INTEGER", nullable: true);

		migrationBuilder.DropColumn(name: "FailedSignIns", table: "AspNetUsers");

		migrationBuilder.AddColumn<bool>(name: "TwoFactorEnabled", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<int>(name: "AccessFailedCount", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.AddColumn<string>(name: "ConcurrencyStamp", table: "AspNetUsers", type: "TEXT", nullable: true);

		migrationBuilder.AddColumn<string>(name: "Email", table: "AspNetUsers", type: "TEXT", maxLength: 256, nullable: true);

		migrationBuilder.AddColumn<bool>(name: "EmailConfirmed", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<bool>(name: "LockoutEnabled", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<string>(name: "NormalizedEmail", table: "AspNetUsers", type: "TEXT", maxLength: 256, nullable: true);

		migrationBuilder.AddColumn<string>(name: "NormalizedUserName", table: "AspNetUsers", type: "TEXT", maxLength: 256, nullable: true);

		migrationBuilder.AddColumn<string>(name: "PhoneNumber", table: "AspNetUsers", type: "TEXT", nullable: true);

		migrationBuilder.AddColumn<bool>(name: "PhoneNumberConfirmed", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: false);

		migrationBuilder.AddColumn<string>(name: "SecurityStamp", table: "AspNetUsers", type: "TEXT", nullable: true);

		migrationBuilder.AddColumn<string>(name: "UserName", table: "AspNetUsers", type: "TEXT", maxLength: 256, nullable: true);

		migrationBuilder.AddPrimaryKey(name: "PK_AspNetUsers", table: "AspNetUsers", column: "Id");

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

		migrationBuilder.CreateIndex(name: "EmailIndex", table: "AspNetUsers", column: "NormalizedEmail");

		migrationBuilder.CreateIndex(name: "UserNameIndex", table: "AspNetUsers", column: "NormalizedUserName", unique: true);

		migrationBuilder.CreateIndex(name: "IX_AspNetRoleClaims_RoleId", table: "AspNetRoleClaims", column: "RoleId");

		migrationBuilder.CreateIndex(name: "RoleNameIndex", table: "AspNetRoles", column: "NormalizedName", unique: true);

		migrationBuilder.CreateIndex(name: "IX_AspNetUserClaims_UserId", table: "AspNetUserClaims", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_AspNetUserLogins_UserId", table: "AspNetUserLogins", column: "UserId");

		migrationBuilder.CreateIndex(name: "IX_AspNetUserRoles_RoleId", table: "AspNetUserRoles", column: "RoleId");

		migrationBuilder.AddForeignKey(
			name: "FK_AccountLinks_AspNetUsers_UserId",
			table: "AccountLinks",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_ApiKeyConfigs_AspNetUsers_UserId",
			table: "ApiKeyConfigs",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Blocklist_AspNetUsers_UserId",
			table: "Blocklist",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Credentials_AspNetUsers_UserId",
			table: "Credentials",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_DeletedFeeds_AspNetUsers_UserId",
			table: "DeletedFeeds",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedAbsences_AspNetUsers_UserId",
			table: "FeedAbsences",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunCalls_AspNetUsers_UserId",
			table: "FeedRunCalls",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunItems_AspNetUsers_UserId",
			table: "FeedRunItems",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRuns_AspNetUsers_UserId",
			table: "FeedRuns",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunTimes_AspNetUsers_UserId",
			table: "FeedRunTimes",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Feeds_AspNetUsers_UserId",
			table: "Feeds",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Jobs_AspNetUsers_OwnerId",
			table: "Jobs",
			column: "OwnerId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationDeliveryOutcomes_AspNetUsers_UserId",
			table: "NotificationDeliveryOutcomes",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationSubscriptions_AspNetUsers_UserId",
			table: "NotificationSubscriptions",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationTargets_AspNetUsers_UserId",
			table: "NotificationTargets",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_RatingDefaults_AspNetUsers_UserId",
			table: "RatingDefaults",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Wantlist_AspNetUsers_UserId",
			table: "Wantlist",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_WantlistOutbox_AspNetUsers_UserId",
			table: "WantlistOutbox",
			column: "UserId",
			principalTable: "AspNetUsers",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey(name: "FK_AccountLinks_AspNetUsers_UserId", table: "AccountLinks");

		migrationBuilder.DropForeignKey(name: "FK_ApiKeyConfigs_AspNetUsers_UserId", table: "ApiKeyConfigs");

		migrationBuilder.DropForeignKey(name: "FK_Blocklist_AspNetUsers_UserId", table: "Blocklist");

		migrationBuilder.DropForeignKey(name: "FK_Credentials_AspNetUsers_UserId", table: "Credentials");

		migrationBuilder.DropForeignKey(name: "FK_DeletedFeeds_AspNetUsers_UserId", table: "DeletedFeeds");

		migrationBuilder.DropForeignKey(name: "FK_FeedAbsences_AspNetUsers_UserId", table: "FeedAbsences");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunCalls_AspNetUsers_UserId", table: "FeedRunCalls");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunItems_AspNetUsers_UserId", table: "FeedRunItems");

		migrationBuilder.DropForeignKey(name: "FK_FeedRuns_AspNetUsers_UserId", table: "FeedRuns");

		migrationBuilder.DropForeignKey(name: "FK_FeedRunTimes_AspNetUsers_UserId", table: "FeedRunTimes");

		migrationBuilder.DropForeignKey(name: "FK_Feeds_AspNetUsers_UserId", table: "Feeds");

		migrationBuilder.DropForeignKey(name: "FK_Jobs_AspNetUsers_OwnerId", table: "Jobs");

		migrationBuilder.DropForeignKey(name: "FK_NotificationDeliveryOutcomes_AspNetUsers_UserId", table: "NotificationDeliveryOutcomes");

		migrationBuilder.DropForeignKey(name: "FK_NotificationSubscriptions_AspNetUsers_UserId", table: "NotificationSubscriptions");

		migrationBuilder.DropForeignKey(name: "FK_NotificationTargets_AspNetUsers_UserId", table: "NotificationTargets");

		migrationBuilder.DropForeignKey(name: "FK_RatingDefaults_AspNetUsers_UserId", table: "RatingDefaults");

		migrationBuilder.DropForeignKey(name: "FK_Wantlist_AspNetUsers_UserId", table: "Wantlist");

		migrationBuilder.DropForeignKey(name: "FK_WantlistOutbox_AspNetUsers_UserId", table: "WantlistOutbox");

		migrationBuilder.DropTable(name: "AspNetRoleClaims");

		migrationBuilder.DropTable(name: "AspNetUserClaims");

		migrationBuilder.DropTable(name: "AspNetUserLogins");

		migrationBuilder.DropTable(name: "AspNetUserRoles");

		migrationBuilder.DropTable(name: "AspNetUserTokens");

		migrationBuilder.DropTable(name: "AspNetRoles");

		migrationBuilder.DropPrimaryKey(name: "PK_AspNetUsers", table: "AspNetUsers");

		migrationBuilder.DropIndex(name: "EmailIndex", table: "AspNetUsers");

		migrationBuilder.DropIndex(name: "UserNameIndex", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "AccessFailedCount", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "ConcurrencyStamp", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "Email", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "EmailConfirmed", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "LockoutEnabled", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "NormalizedEmail", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "NormalizedUserName", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "PhoneNumber", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "PhoneNumberConfirmed", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "SecurityStamp", table: "AspNetUsers");

		migrationBuilder.DropColumn(name: "UserName", table: "AspNetUsers");

		migrationBuilder.RenameTable(name: "AspNetUsers", newName: "Users");

		migrationBuilder.DropColumn(name: "TwoFactorEnabled", table: "Users");

		migrationBuilder.AddColumn<int>(name: "FailedSignIns", table: "Users", type: "INTEGER", nullable: false, defaultValue: 0);

		migrationBuilder.DropColumn(name: "LockoutEnd", table: "Users");

		migrationBuilder.AddColumn<long>(name: "SessionsRevokedAt", table: "Users", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "ExternalUserId", table: "Users", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddColumn<long>(name: "LockedUntil", table: "Users", type: "INTEGER", nullable: true);

		migrationBuilder.AddColumn<string>(name: "Provider", table: "Users", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddColumn<string>(name: "Role", table: "Users", type: "TEXT", nullable: false, defaultValue: "");

		migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", column: "Id");

		migrationBuilder.CreateIndex(name: "IX_Users_Provider_ExternalUserId", table: "Users", columns: new[] { "Provider", "ExternalUserId" }, unique: true);

		migrationBuilder.AddForeignKey(
			name: "FK_AccountLinks_Users_UserId",
			table: "AccountLinks",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_ApiKeyConfigs_Users_UserId",
			table: "ApiKeyConfigs",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Blocklist_Users_UserId",
			table: "Blocklist",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Credentials_Users_UserId",
			table: "Credentials",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_DeletedFeeds_Users_UserId",
			table: "DeletedFeeds",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedAbsences_Users_UserId",
			table: "FeedAbsences",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunCalls_Users_UserId",
			table: "FeedRunCalls",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunItems_Users_UserId",
			table: "FeedRunItems",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRuns_Users_UserId",
			table: "FeedRuns",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_FeedRunTimes_Users_UserId",
			table: "FeedRunTimes",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Feeds_Users_UserId",
			table: "Feeds",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Jobs_Users_OwnerId",
			table: "Jobs",
			column: "OwnerId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationDeliveryOutcomes_Users_UserId",
			table: "NotificationDeliveryOutcomes",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationSubscriptions_Users_UserId",
			table: "NotificationSubscriptions",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_NotificationTargets_Users_UserId",
			table: "NotificationTargets",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_RatingDefaults_Users_UserId",
			table: "RatingDefaults",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_Wantlist_Users_UserId",
			table: "Wantlist",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);

		migrationBuilder.AddForeignKey(
			name: "FK_WantlistOutbox_Users_UserId",
			table: "WantlistOutbox",
			column: "UserId",
			principalTable: "Users",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade
		);
	}
}
