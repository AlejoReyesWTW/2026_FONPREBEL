using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FonPrebel.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfiguration",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfiguration", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Batch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodYear = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Security = table.Column<string>(type: "TEXT", nullable: true),
                    Account = table.Column<string>(type: "TEXT", nullable: true),
                    SenderAddress = table.Column<string>(type: "TEXT", nullable: true),
                    PasswordCipher = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PasswordUpdatedAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConfiguration", x => x.Id);
                    table.CheckConstraint("CK_EmailConfiguration_SingleRow", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "BatchExecution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    LastCheckpointAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    ResumedFromExecutionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchExecution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchExecution_BatchExecution_ResumedFromExecutionId",
                        column: x => x.ResumedFromExecutionId,
                        principalTable: "BatchExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchExecution_Batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BatchRecipient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cedula = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    PdfStatus = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: true),
                    SendStatus = table.Column<string>(type: "TEXT", nullable: false),
                    SendAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmedAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchRecipient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchRecipient_Batch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionInputFingerprint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchExecutionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FileModifiedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionInputFingerprint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionInputFingerprint_BatchExecution_BatchExecutionId",
                        column: x => x.BatchExecutionId,
                        principalTable: "BatchExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhaseExecution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchExecutionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    FinishedAtUtc = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhaseExecution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhaseExecution_BatchExecution_BatchExecutionId",
                        column: x => x.BatchExecutionId,
                        principalTable: "BatchExecution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Batch_Code",
                table: "Batch",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Batch_PeriodYear_PeriodMonth",
                table: "Batch",
                columns: new[] { "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_Batch_Status",
                table: "Batch",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BatchExecution_BatchId_Number",
                table: "BatchExecution",
                columns: new[] { "BatchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchExecution_ResumedFromExecutionId",
                table: "BatchExecution",
                column: "ResumedFromExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchRecipient_BatchId_Cedula",
                table: "BatchRecipient",
                columns: new[] { "BatchId", "Cedula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchRecipient_BatchId_PdfStatus",
                table: "BatchRecipient",
                columns: new[] { "BatchId", "PdfStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BatchRecipient_BatchId_SendStatus",
                table: "BatchRecipient",
                columns: new[] { "BatchId", "SendStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionInputFingerprint_BatchExecutionId_Role",
                table: "ExecutionInputFingerprint",
                columns: new[] { "BatchExecutionId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_PhaseExecution_BatchExecutionId_Phase",
                table: "PhaseExecution",
                columns: new[] { "BatchExecutionId", "Phase" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfiguration");

            migrationBuilder.DropTable(
                name: "BatchRecipient");

            migrationBuilder.DropTable(
                name: "EmailConfiguration");

            migrationBuilder.DropTable(
                name: "ExecutionInputFingerprint");

            migrationBuilder.DropTable(
                name: "PhaseExecution");

            migrationBuilder.DropTable(
                name: "BatchExecution");

            migrationBuilder.DropTable(
                name: "Batch");
        }
    }
}
