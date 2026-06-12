using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SnakkDbContext))]
    [Migration("20260612000001_DmExcerptCiphertextToText")]
    public partial class DmExcerptCiphertextToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LastMessageExcerpt stores IDataProtector ciphertext, not the
            // plaintext excerpt: Protect() adds ~80 bytes of header/IV/HMAC and
            // base64url-expands, so varchar(200) overflowed (22001) for any
            // excerpt past ~70 plaintext chars, failing the whole send. The
            // 200-char plaintext cap is enforced in DmUseCase.
            migrationBuilder.AlterColumn<string>(
                name: "LastMessageExcerpt",
                table: "DmConversation",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Truncate so the narrowing ALTER cannot fail on existing rows
            // (ciphertext over 200 chars is unreadable after truncation either
            // way — the excerpt regenerates on the next message).
            migrationBuilder.Sql("""
                UPDATE "DmConversation"
                SET "LastMessageExcerpt" = left("LastMessageExcerpt", 200)
                WHERE length("LastMessageExcerpt") > 200
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LastMessageExcerpt",
                table: "DmConversation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
