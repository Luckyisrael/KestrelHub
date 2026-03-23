using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KestrelHub.Controller.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GitUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDeployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerInfos",
                columns: table => new
                {
                    ContainerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppDeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageTag = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerInfos", x => x.ContainerId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerInfos_AppDeploymentId",
                table: "ContainerInfos",
                column: "AppDeploymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDeployments");

            migrationBuilder.DropTable(
                name: "ContainerInfos");
        }
    }
}
