using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceParentToUserOutboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                table: "user_outbox_events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_parent",
                table: "user_outbox_events");
        }
    }
}
