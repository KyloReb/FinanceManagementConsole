using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FMC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Suppressed EF Core AddColumns for Device/Organization since they were pre-injected into Db via Raw SQL
            // in Program.cs. Re-running these here would crash the migration.

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");

            // Seed raw SQL logic normalizing string data into explicit Entities protecting data integrity transitions.
            // GROUP BY ensures exactly one canonical Organization entity per unique name — not per user row.
            migrationBuilder.Sql(@"
                INSERT INTO Organizations (Id, Name, Description, IsActive, CreatedAt, IsDeleted, TenantId)
                SELECT NEWID(), Organization, 'Auto-migrated from legacy string infrastructure', 1, GETUTCDATE(), 0, 'SYSTEM'
                FROM AspNetUsers
                WHERE Organization IS NOT NULL AND Organization <> ''
                GROUP BY Organization;
            ");
            
            migrationBuilder.Sql(@"
                UPDATE u
                SET u.OrganizationId = o.Id
                FROM AspNetUsers u
                INNER JOIN Organizations o ON u.Organization = o.Name;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Device",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AspNetUsers");
        }
    }
}
