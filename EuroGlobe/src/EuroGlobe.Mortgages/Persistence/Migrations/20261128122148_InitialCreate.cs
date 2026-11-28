using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroGlobe.Mortgages.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mortgages");

            migrationBuilder.CreateTable(
                name: "euribor_rates",
                schema: "mortgages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    set_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_euribor_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mortgages",
                schema: "mortgages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_value = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    down_payment = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    principal_amount = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    net_monthly_income = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    existing_monthly_debt = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    income_proof_document_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    credit_check_consent = table.Column<bool>(type: "boolean", nullable: false),
                    credit_score = table.Column<int>(type: "integer", nullable: true),
                    ltv_ratio = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    dti_ratio = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    stress_dti_ratio = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    rejection_reasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rate_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    annual_rate = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    margin = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    monthly_payment = table.Column<decimal>(type: "numeric(19,2)", nullable: true),
                    remaining_principal = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    remaining_term_months = table.Column<int>(type: "integer", nullable: false),
                    next_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_subscribed_to_euribor = table.Column<bool>(type: "boolean", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disbursed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delinquent_since = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closure_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    scoring_report = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortgages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mortgage_installments",
                schema: "mortgages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mortgage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    principal_part = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    interest_part = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_amount = table.Column<decimal>(type: "numeric(19,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortgage_installments", x => x.id);
                    table.ForeignKey(
                        name: "fk_mortgage_installments_mortgages_mortgage_id",
                        column: x => x.mortgage_id,
                        principalSchema: "mortgages",
                        principalTable: "mortgages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mortgage_rate_changes",
                schema: "mortgages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mortgage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_euribor = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    new_euribor = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    old_rate = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    new_rate = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    old_payment = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    new_payment = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortgage_rate_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_mortgage_rate_changes_mortgages_mortgage_id",
                        column: x => x.mortgage_id,
                        principalSchema: "mortgages",
                        principalTable: "mortgages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mortgage_state_transitions",
                schema: "mortgages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mortgage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortgage_state_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_mortgage_state_transitions_mortgages_mortgage_id",
                        column: x => x.mortgage_id,
                        principalSchema: "mortgages",
                        principalTable: "mortgages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_euribor_rates_effective_at",
                schema: "mortgages",
                table: "euribor_rates",
                column: "effective_at");

            migrationBuilder.CreateIndex(
                name: "ix_mortgage_installments_mortgage_id_seq",
                schema: "mortgages",
                table: "mortgage_installments",
                columns: new[] { "mortgage_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mortgage_installments_status_due_date",
                schema: "mortgages",
                table: "mortgage_installments",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_mortgage_rate_changes_mortgage_id_at",
                schema: "mortgages",
                table: "mortgage_rate_changes",
                columns: new[] { "mortgage_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_mortgage_state_transitions_mortgage_id_at",
                schema: "mortgages",
                table: "mortgage_state_transitions",
                columns: new[] { "mortgage_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_mortgages_user_id",
                schema: "mortgages",
                table: "mortgages",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "euribor_rates",
                schema: "mortgages");

            migrationBuilder.DropTable(
                name: "mortgage_installments",
                schema: "mortgages");

            migrationBuilder.DropTable(
                name: "mortgage_rate_changes",
                schema: "mortgages");

            migrationBuilder.DropTable(
                name: "mortgage_state_transitions",
                schema: "mortgages");

            migrationBuilder.DropTable(
                name: "mortgages",
                schema: "mortgages");
        }
    }
}
