using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASAssignment.Migrations
{
    /// <inheritdoc />
    public partial class activeAuthToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveAuthToken",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveAuthToken",
                table: "AspNetUsers");
        }
    }
}
