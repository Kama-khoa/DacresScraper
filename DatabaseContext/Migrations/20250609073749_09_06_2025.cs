using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateScraper.Migrations
{
    /// <inheritdoc />
    public partial class _09_06_2025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchEmail",
                table: "Branches");

            migrationBuilder.AlterColumn<string>(
                name: "KeyFeatures",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "UpdateListingUrls",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    ListingUrl = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateListingUrls");

            migrationBuilder.AlterColumn<string>(
                name: "KeyFeatures",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchEmail",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
