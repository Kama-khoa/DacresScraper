using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateScraper.Migrations
{
    /// <inheritdoc />
    public partial class _29_05_2025_Udpate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PropertyId",
                table: "Properties",
                newName: "ListingSiteRef");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "FailedItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "FailedItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BranchAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BranchPostcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchExternalWebsite = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    BranchPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BranchEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.RenameColumn(
                name: "ListingSiteRef",
                table: "Properties",
                newName: "PropertyId");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "FailedItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "FailedItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
