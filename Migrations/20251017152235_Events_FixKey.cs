using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment1.Migrations
{
    public partial class Events_FixKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""Events""') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name  = 'events'
              AND column_name = 'id'
        ) AND NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name  = 'events'
              AND column_name = 'eventid'
        ) THEN
            ALTER TABLE public.""Events"" RENAME COLUMN ""Id"" TO ""EventId"";
        END IF;
    END IF;
END
$$;");
            
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name  = 'events'
          AND column_name = 'price'
          AND (numeric_precision IS NULL OR numeric_scale IS NULL)
    ) THEN
        ALTER TABLE public.""Events""
        ALTER COLUMN ""Price"" TYPE numeric(10,2);
    END IF;
END
$$;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.""Events""') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name  = 'events'
              AND column_name = 'eventid'
        ) THEN
            ALTER TABLE public.""Events"" RENAME COLUMN ""EventId"" TO ""Id"";
        END IF;
    END IF;
END
$$;");
        }
    }
}
