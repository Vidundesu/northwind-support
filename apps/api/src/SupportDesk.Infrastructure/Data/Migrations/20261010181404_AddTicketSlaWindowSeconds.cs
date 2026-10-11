using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaWindowSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaWindowSeconds",
                table: "Tickets",
                type: "int",
                nullable: true);

            // datetime2(3) has millisecond precision. Counting milliseconds avoids the boundary
            // semantics of DATEDIFF(second); the full datetime2 range fits safely in bigint.
            // Floor fractional seconds and leave unknown, non-positive, subsecond, or int-overflow
            // durations NULL.
            migrationBuilder.Sql("""
                UPDATE dbo.Tickets
                SET SlaWindowSeconds = CASE
                    WHEN DueAtUtc IS NULL OR DueAtUtc <= CreatedAtUtc THEN NULL
                    WHEN DATEDIFF_BIG(millisecond, CreatedAtUtc, DueAtUtc) < 1000 THEN NULL
                    WHEN DATEDIFF_BIG(millisecond, CreatedAtUtc, DueAtUtc) > 2147483647000 THEN NULL
                    ELSE CONVERT(int, DATEDIFF_BIG(millisecond, CreatedAtUtc, DueAtUtc) / 1000)
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaWindowSeconds",
                table: "Tickets");
        }
    }
}
