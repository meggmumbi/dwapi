﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Dwapi.ExtractsManagement.Infrastructure.Migrations
{
    public partial class UpdatedTablesCollation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.ToLower().Contains("MySql".ToLower()))
            {
                migrationBuilder.Sql(@"alter table TempHtsEligibilityExtracts convert to character set utf8 collate utf8_unicode_ci;");
                migrationBuilder.Sql(@"alter table TempClientRegistryExtracts convert to character set utf8 collate utf8_unicode_ci;");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
