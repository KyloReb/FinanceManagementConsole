using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FMC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateCardholderTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cardholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdentityUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cardholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cardholders_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cardholders_AccountNumber",
                table: "Cardholders",
                column: "AccountNumber",
                unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Cardholders_OrganizationId",
            table: "Cardholders",
            column: "OrganizationId");

        // Data Migration: Move existing cardholders from AspNetUsers to Cardholders table
        migrationBuilder.Sql(@"
            INSERT INTO Cardholders (Id, AccountNumber, FirstName, LastName, Email, OrganizationId, TenantId, IsActive, CreatedAt, IdentityUserId)
            SELECT 
                NEWID(), 
                ISNULL(u.AccountNumber, ''), 
                ISNULL(u.FirstName, 'Subscriber'), 
                ISNULL(u.LastName, ''), 
                ISNULL(u.Email, ''), 
                ISNULL(u.OrganizationId, '00000000-0000-0000-0000-000000000000'), 
                CAST(ISNULL(u.OrganizationId, '00000000-0000-0000-0000-000000000000') AS NVARCHAR(MAX)), 
                u.IsActive, 
                u.CreatedAt, 
                u.Id
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
            WHERE r.Name = 'User'
        ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cardholders");
        }
    }
}
