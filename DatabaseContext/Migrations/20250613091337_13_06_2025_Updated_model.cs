using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateScraper.Migrations
{
    /// <inheritdoc />
    public partial class _13_06_2025_Updated_model : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchLink",
                table: "PropertyDetails");

            migrationBuilder.DropColumn(
                name: "BranchPhone",
                table: "PropertyDetails");

            migrationBuilder.RenameColumn(
                name: "BannerText",
                table: "PropertyDetails",
                newName: "PriceQualify");

            migrationBuilder.RenameColumn(
                name: "AreaGuide",
                table: "PropertyDetails",
                newName: "AddedDate");

            migrationBuilder.AlterColumn<int>(
                name: "ListingSiteRef",
                table: "PropertyDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "BranchKey",
                table: "Branches",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "BranchEmail",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchEmail",
                table: "Branches");

            migrationBuilder.RenameColumn(
                name: "PriceQualify",
                table: "PropertyDetails",
                newName: "BannerText");

            migrationBuilder.RenameColumn(
                name: "AddedDate",
                table: "PropertyDetails",
                newName: "AreaGuide");

            migrationBuilder.AlterColumn<string>(
                name: "ListingSiteRef",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "BranchLink",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BranchPhone",
                table: "PropertyDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "BranchKey",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
