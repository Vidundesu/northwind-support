using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing agents need a value before the column can be made required.
            // The development seeder replaces this temporary value with a salted hash.
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Agents",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            // Do not leave an empty-string database default on a credential column.
            migrationBuilder.Sql(
                """
                DECLARE @constraintName sysname;
                SELECT @constraintName = dc.name
                FROM sys.default_constraints AS dc
                INNER JOIN sys.columns AS c
                    ON c.object_id = dc.parent_object_id
                   AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Agents]')
                  AND c.name = N'PasswordHash';

                IF @constraintName IS NOT NULL
                BEGIN
                    DECLARE @sql nvarchar(4000) =
                        N'ALTER TABLE [dbo].[Agents] DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
                    EXEC sp_executesql @sql;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Agents");
        }
    }
}
