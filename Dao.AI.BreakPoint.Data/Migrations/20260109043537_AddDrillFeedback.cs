using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dao.AI.BreakPoint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDrillFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeatureImportanceJson",
                table: "AnalysisResults");

            migrationBuilder.AddColumn<int>(
                name: "BackswingScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContactScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowThroughScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrepScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DrillRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalysisResultId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    TargetPhase = table.Column<int>(type: "integer", nullable: false),
                    TargetFeature = table.Column<string>(type: "text", nullable: false),
                    DrillName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SuggestedDuration = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThumbsUp = table.Column<bool>(type: "boolean", nullable: true),
                    FeedbackText = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAppUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByAppUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrillRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrillRecommendations_AnalysisResults_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "AnalysisResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrillRecommendations_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhaseDeviations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalysisResultId = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAppUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhaseDeviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhaseDeviations_AnalysisResults_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "AnalysisResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureDeviations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhaseDeviationId = table.Column<int>(type: "integer", nullable: false),
                    FeatureIndex = table.Column<int>(type: "integer", nullable: false),
                    FeatureName = table.Column<string>(type: "text", nullable: false),
                    ZScore = table.Column<double>(type: "double precision", nullable: false),
                    ActualValue = table.Column<double>(type: "double precision", nullable: false),
                    ReferenceMean = table.Column<double>(type: "double precision", nullable: false),
                    ReferenceStd = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAppUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDeviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureDeviations_PhaseDeviations_PhaseDeviationId",
                        column: x => x.PhaseDeviationId,
                        principalTable: "PhaseDeviations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrillRecommendations_AnalysisResultId",
                table: "DrillRecommendations",
                column: "AnalysisResultId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillRecommendations_PlayerId",
                table: "DrillRecommendations",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDeviations_PhaseDeviationId",
                table: "FeatureDeviations",
                column: "PhaseDeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhaseDeviations_AnalysisResultId",
                table: "PhaseDeviations",
                column: "AnalysisResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrillRecommendations");

            migrationBuilder.DropTable(
                name: "FeatureDeviations");

            migrationBuilder.DropTable(
                name: "PhaseDeviations");

            migrationBuilder.DropColumn(
                name: "BackswingScore",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "ContactScore",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "FollowThroughScore",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "PrepScore",
                table: "AnalysisResults");

            migrationBuilder.AddColumn<string>(
                name: "FeatureImportanceJson",
                table: "AnalysisResults",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
