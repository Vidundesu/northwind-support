using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSlaWindowSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveSlaWindowSeconds",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE dbo.Tickets SET ActiveSlaWindowSeconds = SlaWindowSeconds WHERE SlaWindowSeconds > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveSlaWindowSeconds",
                table: "Tickets");
        }
    }
}
