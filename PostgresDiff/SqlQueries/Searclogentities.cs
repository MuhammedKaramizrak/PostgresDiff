using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostgresDiff
{

    public class Searchlogentities : ISQLQuery
    {
        public string sqlquerytext { get { return sqlq; } }
        public static string sqlq = @"
CREATE OR REPLACE FUNCTION search_log_entries(
    search_time TIMESTAMP,
    search_text TEXT
)
RETURNS TABLE(startline BIGINT, endline BIGINT, log_block TEXT) 
LANGUAGE plpgsql AS $$
DECLARE
    log_size BIGINT;
    log_path TEXT;
    log_template TEXT;
BEGIN
    -- LOG YOLUNU DİNAMİK BUL
    SELECT current_setting('log_filename') INTO log_template;

    log_template := replace(log_template, '%Y', to_char(now(), 'YYYY'));
log_template := replace(log_template, '%m', to_char(now(), 'MM'));
log_template := replace(log_template, '%d', to_char(now(), 'DD'));
log_template := replace(log_template, '%H', '00');
log_template := replace(log_template, '%M', '00');
log_template := replace(log_template, '%S', '00');

    SELECT 
        CASE 
            WHEN current_setting('log_directory') LIKE '/%' 
                THEN current_setting('log_directory') || '/' || filename
            ELSE current_setting('data_directory') || '/' || current_setting('log_directory') || '/' || filename
        END
    INTO log_path
    FROM (
        SELECT name as filename   --select * from pg_ls_logdir()
        FROM pg_ls_logdir()
        WHERE name LIKE 'postgresql-%.log'
        ORDER BY modification DESC
        LIMIT 1
    ) AS latest;

    -- Log dosyasının boyutunu al
    SELECT (pg_stat_file(log_path)).size INTO log_size;

    -- LOG PARÇALAMA VE FİLTRELEME
    RETURN QUERY 
    WITH log_lines AS (
        SELECT row_number() OVER () AS rn, line
        FROM unnest(
            string_to_array(
                pg_read_file(
                    log_path,
                    GREATEST(log_size - 50000, 0),
                    50000
                ),
                E'\n'
            )
        ) AS t(line)
    ),
    timestamps AS (
    SELECT rn AS startline
    FROM log_lines
    WHERE line ~ '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s*[+-]\d{2})?'

    ),
endlines AS (
    SELECT 
        t1.startline, 
        COALESCE(
            (SELECT MIN(t2.startline) - 1 
             FROM timestamps t2 
             WHERE t2.startline > t1.startline),
            (SELECT MAX(rn) FROM log_lines)
        ) AS endline
    FROM timestamps t1
),
filtered_times AS (
    SELECT e.startline, e.endline
    FROM endlines e
    JOIN log_lines l ON l.rn = e.startline
    WHERE to_timestamp(SUBSTRING(l.line FROM '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}'), 'YYYY-MM-DD HH24:MI:SS') >= search_time
),
  matched_blocks AS (
    SELECT f.startline, f.endline
    FROM filtered_times f
    JOIN log_lines l ON l.rn = f.startline
    WHERE LOWER(l.line) LIKE LOWER('%' || search_text || '%')
)
    SELECT m.startline, m.endline, STRING_AGG(l.line, E'\n') AS log_block
    FROM matched_blocks m
    JOIN log_lines l ON l.rn BETWEEN m.startline AND m.endline
    GROUP BY m.startline, m.endline;
END;
$$;
--select * from search_log_entries('2025-06-20 14:33:13.941', 'search_log_entries(timestamp without time zone,pg_catalog.text)');
";
    }
}
