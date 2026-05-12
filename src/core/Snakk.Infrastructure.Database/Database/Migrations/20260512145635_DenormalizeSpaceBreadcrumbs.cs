using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeSpaceBreadcrumbs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommunityName",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityPublicId",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunitySlug",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubName",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubPublicId",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubSlug",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Space" sp
                SET "HubPublicId"       = h."PublicId",
                    "HubSlug"           = h."Slug",
                    "HubName"           = h."Name",
                    "CommunityPublicId" = c."PublicId",
                    "CommunitySlug"     = c."Slug",
                    "CommunityName"     = c."Name"
                FROM "Hub" h
                JOIN "Community" c ON c."Id" = h."CommunityId" AND NOT c."IsDeleted"
                WHERE sp."HubId" = h."Id" AND NOT sp."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunityName",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "CommunityPublicId",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "CommunitySlug",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "HubName",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "HubPublicId",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "HubSlug",
                table: "Space");
        }
    }
}
