using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WanderMeet.Api.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePendingInviteIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_invites_sender_receiver_pending_unique",
                table: "invites",
                columns: new[] { "sender_id", "receiver_id" },
                unique: true,
                filter: "\"status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_invites_sender_receiver_pending_unique",
                table: "invites");
        }
    }
}
