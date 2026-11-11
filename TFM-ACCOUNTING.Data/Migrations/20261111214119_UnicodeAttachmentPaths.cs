using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TFM_ACCOUNTING.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnicodeAttachmentPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Attachments] WHERE DATALENGTH(CONVERT(nvarchar(max), [RelativePath])) > 1700)
                    THROW 50001, 'An attachment path exceeds the Unicode column length. Shorten it before migrating.', 1;
                """);
            migrationBuilder.DropCheckConstraint("CK_Attachments_Path", "Attachments");
            migrationBuilder.AlterColumn<string>(
                name: "RelativePath",
                table: "Attachments",
                type: "nvarchar(850)",
                maxLength: 850,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldUnicode: false,
                oldMaxLength: 1000);
            migrationBuilder.AddCheckConstraint("CK_Attachments_Path", "Attachments", "LEN(LTRIM(RTRIM([RelativePath]))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [Attachments]
                    WHERE DATALENGTH(CONVERT(varchar(max), [RelativePath])) > 1000
                       OR CONVERT(varbinary(max), CONVERT(nvarchar(max), CONVERT(varchar(max), [RelativePath])))
                           <> CONVERT(varbinary(max), [RelativePath])
                )
                    THROW 50002, 'Reverting would alter stored attachment paths.', 1;
                """);
            migrationBuilder.DropCheckConstraint("CK_Attachments_Path", "Attachments");
            migrationBuilder.AlterColumn<string>(
                name: "RelativePath",
                table: "Attachments",
                type: "varchar(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(850)",
                oldMaxLength: 850);
            migrationBuilder.AddCheckConstraint("CK_Attachments_Path", "Attachments", "LEN(LTRIM(RTRIM([RelativePath]))) > 0");
        }
    }
}
