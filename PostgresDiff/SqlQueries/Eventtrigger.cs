
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostgresDiff
{

    public class Eventtrigger : ISQLQuery
    {
        public static string sqlquerytext { get { return sqlq; } }
        private static string sqlq = @"
---DROP TABLE IF EXISTS ddl_posgresqllog ;

---drop sequence if exists ddl_posgresqllog_id_seq;
-- Sequence oluşturuluyor (Tablo henüz olmadığı için OWNED BY kullanmıyoruz)
CREATE SEQUENCE IF NOT EXISTS ddl_posgresqllog_id_seq;

-- Yeni tablo oluşturuluyor
CREATE table if not exists ddl_posgresqllog (
    id INTEGER PRIMARY KEY DEFAULT nextval('ddl_posgresqllog_id_seq'),
    logtime TIMESTAMP,
    object_schema TEXT,
    object_type text,
    object_name TEXT,
    logcommand TEXT
);

-- Sequence'i tabloya bağlıyoruz
ALTER sequence  ddl_posgresqllog_id_seq OWNED BY ddl_posgresqllog.id;

-- Index oluşturuluyor
CREATE index if not exists idx_object_schema_name ON ddl_posgresqllog (object_schema, object_name);


---select * from ddl_posgresqllog

---drop function search_log_entries;



DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_event_trigger WHERE evtname = 'track_ddl_changes'
    ) THEN
        CREATE EVENT TRIGGER track_ddl_changes
        ON ddl_command_end
        EXECUTE FUNCTION log_ddl_changes();
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_type WHERE typname = 'tabledefs'
    ) THEN
        CREATE TYPE public.tabledefs AS ENUM (
            'PKEY_INTERNAL', 'PKEY_EXTERNAL', 'FKEYS_INTERNAL', 'FKEYS_EXTERNAL', 'COMMENTS',
            'FKEYS_NONE', 'INCLUDE_TRIGGERS', 'NO_TRIGGERS', 'SHOWPARTS',
            'ACL_OWNER', 'ACL_DCL', 'ACL_POLICIES'
        );
    END IF;
END$$;
";
    }
}

