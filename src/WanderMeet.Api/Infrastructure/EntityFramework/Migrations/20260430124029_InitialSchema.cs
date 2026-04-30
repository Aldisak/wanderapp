using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WanderMeet.Api.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    location = table.Column<Point>(type: "geography (Point, 4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hangout_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    emoji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hangout_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "places",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_place_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<Point>(type: "geography (Point, 4326)", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    has_wifi = table.Column<bool>(type: "boolean", nullable: false),
                    is_quiet = table.Column<bool>(type: "boolean", nullable: false),
                    is_solo_friendly = table.Column<bool>(type: "boolean", nullable: false),
                    google_rating = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: true),
                    wander_meetup_count = table.Column<int>(type: "integer", nullable: false),
                    is_sponsored = table.Column<bool>(type: "boolean", nullable: false),
                    sponsor_perk = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_places", x => x.id);
                    table.ForeignKey(
                        name: "fk_places_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    azure_ad_b2c_id = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bio = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_id_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_open_today = table.Column<bool>(type: "boolean", nullable: false),
                    is_open_to_romance = table.Column<bool>(type: "boolean", nullable: false),
                    last_active_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<Point>(type: "geography (Point, 4326)", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trust_score = table.Column<int>(type: "integer", nullable: false),
                    meetup_count = table.Column<int>(type: "integer", nullable: false),
                    cities_count = table.Column<int>(type: "integer", nullable: false),
                    years_nomading = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hangout_tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    place_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_is_there = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_invites_hangout_tags_hangout_tag_id",
                        column: x => x.hangout_tag_id,
                        principalTable: "hangout_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invites_places_place_id",
                        column: x => x.place_id,
                        principalTable: "places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invites_users_receiver_id",
                        column: x => x.receiver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invites_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_users_reported_id",
                        column: x => x.reported_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    departed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_cities", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_cities_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_cities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_hangout_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hangout_tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_hangout_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_hangout_tags_hangout_tags_hangout_tag_id",
                        column: x => x.hangout_tag_id,
                        principalTable: "hangout_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_hangout_tags_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blob_url = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_photos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meetups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    place_id = table.Column<Guid>(type: "uuid", nullable: false),
                    met_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prompt_sent = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meetups", x => x.id);
                    table.ForeignKey(
                        name: "fk_meetups_invites_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meetups_places_place_id",
                        column: x => x.place_id,
                        principalTable: "places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meetups_users_user_a_id",
                        column: x => x.user_a_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meetups_users_user_b_id",
                        column: x => x.user_b_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meetup_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meetup_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    did_meet = table.Column<bool>(type: "boolean", nullable: false),
                    felt_safe = table.Column<bool>(type: "boolean", nullable: false),
                    good_convo = table.Column<bool>(type: "boolean", nullable: false),
                    would_meet_again = table.Column<bool>(type: "boolean", nullable: false),
                    text = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meetup_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_meetup_reviews_meetups_meetup_id",
                        column: x => x.meetup_id,
                        principalTable: "meetups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meetup_reviews_users_reviewee_id",
                        column: x => x.reviewee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meetup_reviews_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "hangout_tags",
                columns: new[] { "id", "created_at", "deleted_at", "emoji", "label", "slug", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "☕", "Coffee", "Coffee", null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "🚶", "Walk", "Walk", null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "🍽️", "Food", "Food", null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "🗺️", "Explore", "Explore", null },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "💻", "Cowork", "Cowork", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_cities_location",
                table: "cities",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_cities_name_country",
                table: "cities",
                columns: new[] { "name", "country" });

            migrationBuilder.CreateIndex(
                name: "ix_hangout_tags_slug",
                table: "hangout_tags",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invites_hangout_tag_id",
                table: "invites",
                column: "hangout_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_place_id",
                table: "invites",
                column: "place_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_receiver_id",
                table: "invites",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_receiver_id_status",
                table: "invites",
                columns: new[] { "receiver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_sender_id",
                table: "invites",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_sender_id_status",
                table: "invites",
                columns: new[] { "sender_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_status_expires_at",
                table: "invites",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meetup_reviews_meetup_id",
                table: "meetup_reviews",
                column: "meetup_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetup_reviews_meetup_id_reviewer_id",
                table: "meetup_reviews",
                columns: new[] { "meetup_id", "reviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meetup_reviews_reviewee_id",
                table: "meetup_reviews",
                column: "reviewee_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetup_reviews_reviewer_id",
                table: "meetup_reviews",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetups_invite_id",
                table: "meetups",
                column: "invite_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meetups_place_id",
                table: "meetups",
                column: "place_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetups_prompt_sent_met_at",
                table: "meetups",
                columns: new[] { "prompt_sent", "met_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meetups_user_a_id",
                table: "meetups",
                column: "user_a_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetups_user_b_id",
                table: "meetups",
                column: "user_b_id");

            migrationBuilder.CreateIndex(
                name: "ix_places_city_id",
                table: "places",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_places_city_id_category",
                table: "places",
                columns: new[] { "city_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_places_google_place_id",
                table: "places",
                column: "google_place_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_places_location",
                table: "places",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_places_wander_meetup_count",
                table: "places",
                column: "wander_meetup_count");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reported_id",
                table: "reports",
                column: "reported_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id",
                table: "reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reviewed_at_created_at",
                table: "reports",
                columns: new[] { "reviewed_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_cities_city_id",
                table: "user_cities",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_cities_user_id",
                table: "user_cities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_cities_user_id_departed_at",
                table: "user_cities",
                columns: new[] { "user_id", "departed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_hangout_tags_hangout_tag_id",
                table: "user_hangout_tags",
                column: "hangout_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_hangout_tags_user_id_hangout_tag_id",
                table: "user_hangout_tags",
                columns: new[] { "user_id", "hangout_tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_photos_user_id_order",
                table: "user_photos",
                columns: new[] { "user_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_azure_ad_b2c_id",
                table: "users",
                column: "azure_ad_b2c_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_city_id",
                table: "users",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_city_id_is_open_today_last_active_at",
                table: "users",
                columns: new[] { "city_id", "is_open_today", "last_active_at" });

            migrationBuilder.CreateIndex(
                name: "ix_users_deleted_at",
                table: "users",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_users_location",
                table: "users",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_users_trust_score",
                table: "users",
                column: "trust_score");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meetup_reviews");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "user_cities");

            migrationBuilder.DropTable(
                name: "user_hangout_tags");

            migrationBuilder.DropTable(
                name: "user_photos");

            migrationBuilder.DropTable(
                name: "meetups");

            migrationBuilder.DropTable(
                name: "invites");

            migrationBuilder.DropTable(
                name: "hangout_tags");

            migrationBuilder.DropTable(
                name: "places");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "cities");
        }
    }
}
