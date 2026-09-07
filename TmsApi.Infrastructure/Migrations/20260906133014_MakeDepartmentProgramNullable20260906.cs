using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmsApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeDepartmentProgramNullable20260906 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Courses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Programs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Courses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Programs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );
        }
    }
}
