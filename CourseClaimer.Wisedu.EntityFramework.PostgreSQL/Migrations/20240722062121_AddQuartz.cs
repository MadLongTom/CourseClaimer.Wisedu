﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseClaimer.Wisedu.EntityFramework.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddQuartz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecuteTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    JobName = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRecords");
        }
    }
}
