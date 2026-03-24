using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KestrelHub.Controller.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedDomain",
                table: "AppDeployments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedPort",
                table: "AppDeployments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedDomain",
                table: "AppDeployments");

            migrationBuilder.DropColumn(
                name: "AssignedPort",
                table: "AppDeployments");
        }
    }
}
