using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroGlobe.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "job_runs",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    items_processed = table.Column<int>(type: "integer", nullable: false),
                    items_failed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_job_name_started_at",
                schema: "jobs",
                table: "job_runs",
                columns: new[] { "job_name", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_job_name_trigger_scheduled_for",
                schema: "jobs",
                table: "job_runs",
                columns: new[] { "job_name", "trigger", "scheduled_for" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs",
                schema: "jobs");
        }
    }
}
