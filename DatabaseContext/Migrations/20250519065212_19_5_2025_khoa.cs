using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateScraper.Migrations
{
    /// <inheritdoc />
    public partial class _19_5_2025_khoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BannerText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ListingSiteRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bedrooms = table.Column<int>(type: "int", nullable: true),
                    Bathrooms = table.Column<int>(type: "int", nullable: true),
                    Receptions = table.Column<int>(type: "int", nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchLink = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullBrochure = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Tenure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyFeatures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FloorPlan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreaGuide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VirtualTour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunSession = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDetails", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyDetails");
        }
    }
}
