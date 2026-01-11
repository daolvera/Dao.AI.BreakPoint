using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dao.AI.BreakPoint.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePrepScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrepScore",
                table: "AnalysisResults");

            migrationBuilder.AddColumn<string>(
                name: "TrainingHistorySummary",
                table: "Players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainingHistorySummary",
                table: "Players");

            migrationBuilder.AddColumn<int>(
                name: "PrepScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
