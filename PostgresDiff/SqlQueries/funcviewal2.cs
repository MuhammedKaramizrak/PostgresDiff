using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostgresDiff
{
    public interface ISQLQuery
    {
        static string sqlquerytext { get; }
    }
    public class funcviewal2 : ISQLQuery
    {
        public static string sqlquerytext { get { return sqlq; } }
        private static string sqlq = @"CREATE OR REPLACE FUNCTION public.funcviewgonder2(
    she text,
    gobject_type text default null,
    gobjid oid default null
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    fun_rec record;
    l_rec record;
    l_stmt text;
    sqltext text;
    rsqltext text;
    objectadi text;
    alttip text;
    cache_exists boolean;
BEGIN
    -- Tablo var mı kontrol et
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'funcviewtablemastercache'
    ) INTO cache_exists;

    -- Yoksa oluştur
    IF NOT cache_exists THEN
        EXECUTE $create$
        CREATE TABLE public.funcviewtablemastercache (
            objecttype text,
            objectadi text,
            sqltext text,
            rsqltext text
        )
        $create$;
    END IF;

    -- Temizle
    DELETE FROM public.funcviewtablemastercache;

    -- FUNCTION / PROCEDURE
    IF gobject_type IS NULL OR gobject_type ILIKE ANY(ARRAY['function','procedure']) THEN
        FOR fun_rec IN (
            SELECT
                CASE prokind
                    WHEN 'f' THEN 'FUNCTION'
                    WHEN 'p' THEN 'PROCEDURE'
                    ELSE NULL
                END AS alttip,
                (SELECT pg_get_functiondef(p.oid)) AS sqltext,
                format('DROP %s IF EXISTS %s;', 
                    CASE prokind
                        WHEN 'f' THEN 'FUNCTION'
                        WHEN 'p' THEN 'PROCEDURE'
                    END,
                    p.oid::regprocedure
                ) AS rsqltext,
                p.oid::regprocedure::text AS objectname
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            JOIN pg_language l ON l.oid = p.prolang
            LEFT JOIN pg_extension e ON n.nspname = e.extname
            WHERE n.nspname = she
              AND e.extname IS NULL
              AND l.lanname = 'plpgsql'
              AND prokind IN ('f', 'p')
              AND proname NOT IN (
                '_insert2table', '_i2utable', 'listtables', 'random_between',
                'owneral', 'sorttablesbydependency', '_update2table', 'view2func',
                'rez_asyavalidate', 'drop_all_user_objects', 'beforefunc',
                'fatura_asyavalidate', 'getmaliyethesap', 'musteri_asyavalidate', 'ef2tablo'
              )
              AND (gobject_type IS NULL OR LOWER(gobject_type) = CASE prokind WHEN 'f' THEN 'function' WHEN 'p' THEN 'procedure' END)
              AND (gobjid IS NULL OR gobjid = p.oid)
        ) LOOP
            INSERT INTO public.funcviewtablemastercache(objecttype, objectadi, sqltext, rsqltext)
            VALUES (fun_rec.alttip, she || '.' || fun_rec.objectname, fun_rec.sqltext || ';', fun_rec.rsqltext);
        END LOOP;
    END IF;
   -- TYPES
if gobject_type is null or LOWER(gobject_type) = 'type' then
  for l_rec in (
    SELECT
      n.nspname AS schema_name,
      t.typname AS type_name,
      string_agg(quote_literal(e.enumlabel), ', ') AS enum_labels
    FROM pg_type t
    JOIN pg_enum e ON t.oid = e.enumtypid
    JOIN pg_namespace n ON n.oid = t.typnamespace
    WHERE t.typtype = 'e' AND n.nspname = she  -- sadece ilgili şema
    GROUP BY n.nspname, t.typname
  )
  loop
    sqltext := 'CREATE TYPE ' || quote_ident(l_rec.schema_name) || '.' || quote_ident(l_rec.type_name)
               || ' AS ENUM (' || l_rec.enum_labels || ');';
    alttip := 'TYPE';
    objectadi := l_rec.schema_name || '.' || l_rec.type_name;
    rsqltext := 'DROP TYPE IF EXISTS ' || quote_ident(l_rec.schema_name) || '.' || quote_ident(l_rec.type_name) || ' CASCADE;';
    
    INSERT INTO public.funcviewtablemastercache(objecttype, objectadi, sqltext, rsqltext)
    VALUES (alttip, objectadi, sqltext, rsqltext);
  end loop;
end if;

    -- VIEW
    IF gobject_type IS NULL OR LOWER(gobject_type) = 'view' THEN
        FOR l_rec IN (
            SELECT v.schemaname, v.viewname, c.oid
            FROM pg_views v
            JOIN pg_class c ON c.relname = v.viewname AND c.relkind = 'v'
            WHERE v.schemaname = she
              AND (gobject_type IS NULL OR gobject_type = 'view')
              AND (gobjid IS NULL OR c.oid = gobjid)
        ) LOOP
            sqltext := 'CREATE OR REPLACE VIEW ' || quote_ident(l_rec.schemaname) || '.' || quote_ident(l_rec.viewname) || ' AS ' ||
                       pg_get_viewdef(l_rec.schemaname || '.' || l_rec.viewname, true);
            rsqltext := format('DROP VIEW IF EXISTS %I.%I;', l_rec.schemaname, l_rec.viewname);
            objectadi := l_rec.schemaname || '.' || l_rec.viewname;

            INSERT INTO public.funcviewtablemastercache(objecttype, objectadi, sqltext, rsqltext)
            VALUES ('VIEW', objectadi, sqltext || ';', rsqltext);
        END LOOP;
    END IF;
    -- TABLE
    IF gobject_type IS NULL OR LOWER(gobject_type) = 'table' THEN
        FOR l_rec IN (
            SELECT def AS tdef, in_table AS tabadi
            FROM pg_get_tabledeftum(she, false)
            WHERE gobjid IS NULL  OR in_table::regclass::oid = gobjid
        ) LOOP
            sqltext := l_rec.tdef;
            objectadi := l_rec.tabadi;
            rsqltext := 'DROP TABLE IF EXISTS ' || l_rec.tabadi || ';';

            INSERT INTO public.funcviewtablemastercache(objecttype, objectadi, sqltext, rsqltext)
            VALUES ('TABLE', objectadi, sqltext || ';', rsqltext);
        END LOOP;
    END IF;
    -- TRIGGER
    IF gobject_type IS NULL OR LOWER(gobject_type) = 'trigger' THEN
        FOR l_rec IN (
            SELECT 
                tg.tgname AS trigger_name,
                n.nspname AS schema_name,
                c.relname AS table_name,
                pg_get_triggerdef(tg.oid) AS create_trigger_query,
                'DROP TRIGGER IF EXISTS ' || quote_ident(tg.tgname) || 
                ' ON ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ';' AS drop_trigger_query
            FROM pg_trigger tg
            JOIN pg_class c ON tg.tgrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE NOT tg.tgisinternal
              AND n.nspname = she
              AND (gobject_type IS NULL OR gobject_type = 'trigger')
              AND (gobjid IS NULL OR tg.oid = gobjid)
        ) LOOP
            objectadi := l_rec.schema_name || '.' || l_rec.trigger_name;

            INSERT INTO public.funcviewtablemastercache(objecttype, objectadi, sqltext, rsqltext)
            VALUES ('TRIGGER', objectadi, l_rec.create_trigger_query || ';', l_rec.drop_trigger_query || ';');
        END LOOP;
    END IF;
  END;
$$;
";
    }
}
