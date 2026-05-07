using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WanderMeet.Api.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class UserPhotoSoftDeleteAwareUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_photos_user_id_order",
                table: "user_photos");

            migrationBuilder.CreateIndex(
                name: "ix_user_photos_user_id_order",
                table: "user_photos",
                columns: new[] { "user_id", "order" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_photos_user_id_order",
                table: "user_photos");

            migrationBuilder.CreateIndex(
                name: "ix_user_photos_user_id_order",
                table: "user_photos",
                columns: new[] { "user_id", "order" },
                unique: true);
        }
    }
}
