using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmsApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentProgramTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(150)",
                        maxLength: 150,
                        nullable: false
                    ),
                    Code = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: true
                    ),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(150)",
                        maxLength: 150,
                        nullable: false
                    ),
                    Code = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: true
                    ),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Programs_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Courses_DepartmentId",
                table: "Courses",
                column: "DepartmentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ProgramId",
                table: "Courses",
                column: "ProgramId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Programs_Code",
                table: "Programs",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Programs_DepartmentId_Name",
                table: "Programs",
                columns: new[] { "DepartmentId", "Name" },
                unique: true
            );

            migrationBuilder.Sql(
                @"
INSERT INTO ""Departments"" (""Id"", ""Name"", ""Code"", ""Description"", ""IsActive"", ""CreatedAt"")
VALUES
    ('11111111-1111-4111-8111-111111111111'::uuid, 'Computer Science', 'CS', 'Computer Science department', TRUE, CURRENT_TIMESTAMP),
    ('22222222-2222-4222-8222-222222222222'::uuid, 'Engineering', 'ENG', 'Engineering department', TRUE, CURRENT_TIMESTAMP),
    ('33333333-3333-4333-8333-333333333333'::uuid, 'Business', 'BUS', 'Business department', TRUE, CURRENT_TIMESTAMP),
    ('44444444-4444-4444-8444-444444444444'::uuid, 'Health Sciences', 'HS', 'Health Sciences department', TRUE, CURRENT_TIMESTAMP);

INSERT INTO ""Programs"" (""Id"", ""Name"", ""Code"", ""Description"", ""DepartmentId"", ""IsActive"", ""CreatedAt"")
VALUES
    ('51111111-1111-5111-8111-111111111111'::uuid, 'BSc Computer Science', 'BSC-CS', 'BSc Computer Science program', '11111111-1111-4111-8111-111111111111'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('52222222-2222-5222-8222-222222222222'::uuid, 'BSc Software Engineering', 'BSC-SE', 'BSc Software Engineering program', '11111111-1111-4111-8111-111111111111'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('53333333-3333-5333-8333-333333333333'::uuid, 'BSc Data Science', 'BSC-DS', 'BSc Data Science program', '11111111-1111-4111-8111-111111111111'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('61111111-1111-6111-8111-111111111111'::uuid, 'BSc Electrical Engineering', 'BSC-EE', 'BSc Electrical Engineering program', '22222222-2222-4222-8222-222222222222'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('62222222-2222-6222-8222-222222222222'::uuid, 'BSc Mechanical Engineering', 'BSC-ME', 'BSc Mechanical Engineering program', '22222222-2222-4222-8222-222222222222'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('63333333-3333-6333-8333-333333333333'::uuid, 'BSc Civil Engineering', 'BSC-CE', 'BSc Civil Engineering program', '22222222-2222-4222-8222-222222222222'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('71111111-1111-7111-8111-111111111111'::uuid, 'BBA Management', 'BBA-MGT', 'BBA Management program', '33333333-3333-4333-8333-333333333333'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('72222222-2222-7222-8222-222222222222'::uuid, 'BBA Marketing', 'BBA-MKT', 'BBA Marketing program', '33333333-3333-4333-8333-333333333333'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('73333333-3333-7333-8333-333333333333'::uuid, 'BBA Finance', 'BBA-FIN', 'BBA Finance program', '33333333-3333-4333-8333-333333333333'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('81111111-1111-8111-8111-111111111111'::uuid, 'BSc Nursing', 'BSC-NUR', 'BSc Nursing program', '44444444-4444-4444-8444-444444444444'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('82222222-2222-8222-8222-222222222222'::uuid, 'BSc Public Health', 'BSC-PH', 'BSc Public Health program', '44444444-4444-4444-8444-444444444444'::uuid, TRUE, CURRENT_TIMESTAMP),
    ('83333333-3333-8333-8333-333333333333'::uuid, 'BSc Pharmacy', 'BSC-PHR', 'BSc Pharmacy program', '44444444-4444-4444-8444-444444444444'::uuid, TRUE, CURRENT_TIMESTAMP);

UPDATE ""Courses""
SET ""DepartmentId"" = CASE
    WHEN ""CourseCode"" LIKE 'CS%' THEN '11111111-1111-4111-8111-111111111111'::uuid
    WHEN ""CourseCode"" LIKE 'ENG%' THEN '22222222-2222-4222-8222-222222222222'::uuid
    WHEN ""CourseCode"" LIKE 'BUS%' THEN '33333333-3333-4333-8333-333333333333'::uuid
    WHEN ""CourseCode"" LIKE 'HS%' THEN '44444444-4444-4444-8444-444444444444'::uuid
    ELSE '11111111-1111-4111-8111-111111111111'::uuid
END,
    ""ProgramId"" = CASE
    WHEN ""CourseCode"" LIKE 'CS%' THEN '51111111-1111-5111-8111-111111111111'::uuid
    WHEN ""CourseCode"" LIKE 'ENG%' THEN '61111111-1111-6111-8111-111111111111'::uuid
    WHEN ""CourseCode"" LIKE 'BUS%' THEN '71111111-1111-7111-8111-111111111111'::uuid
    WHEN ""CourseCode"" LIKE 'HS%' THEN '81111111-1111-8111-8111-111111111111'::uuid
    ELSE NULL
END;
"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Departments_DepartmentId",
                table: "Courses",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Programs_ProgramId",
                table: "Courses",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Departments_DepartmentId",
                table: "Courses"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Programs_ProgramId",
                table: "Courses"
            );

            migrationBuilder.DropTable(name: "Programs");

            migrationBuilder.DropTable(name: "Departments");

            migrationBuilder.DropIndex(name: "IX_Courses_DepartmentId", table: "Courses");

            migrationBuilder.DropIndex(name: "IX_Courses_ProgramId", table: "Courses");
        }
    }
}
