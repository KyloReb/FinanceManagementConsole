using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FMC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActionDate",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverId",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MakerId",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ApproverId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MakerId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Transactions");
        }
    }
}
