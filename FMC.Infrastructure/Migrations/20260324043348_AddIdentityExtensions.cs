using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FMC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Robust check for RefreshToken columns
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = N'RefreshToken')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [RefreshToken] nvarchar(max) NULL;
                END
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = N'RefreshTokenExpiryTime')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [RefreshTokenExpiryTime] datetime2 NULL;
                END
            ");

            // Robust check for UserOtpVerifications table
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[UserOtpVerifications]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [UserOtpVerifications] (
                        [Id] uniqueidentifier NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [OtpCode] nvarchar(max) NOT NULL,
                        [OtpType] nvarchar(max) NOT NULL,
                        [ExpiresAt] datetime2 NOT NULL,
                        [IsUsed] bit NOT NULL,
                        [FailedAttempts] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [IpAddress] nvarchar(max) NULL,
                        CONSTRAINT [PK_UserOtpVerifications] PRIMARY KEY ([Id])
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserOtpVerifications");

            migrationBuilder.DropColumn(name: "RefreshToken", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "RefreshTokenExpiryTime", table: "AspNetUsers");
        }
    }
}
