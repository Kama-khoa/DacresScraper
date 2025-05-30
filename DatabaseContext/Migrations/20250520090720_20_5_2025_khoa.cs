using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateScraper.Migrations
{
    /// <inheritdoc />
    public partial class _20_5_2025_khoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EPC",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EPC",
                table: "PropertyDetails");
        }
    }
}
