using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PostgresDiff.DdlComparatorControl;

namespace PostgresDiff
{
    public static class DatabaseSchemaService
    {
        public static async Task EnsureInitializedAsync(DdlComparatorControl cont, ConnectionItem connection, NpgsqlConnection conn)
        {
            using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM pg_proc WHERE proname = 'funcviewtablemastercache'", conn))
            {
                var count = (long)(await checkCmd.ExecuteScalarAsync());
                if (count == 0 || 1 == 1)
                {
                    await ExecuteScriptTextAsync(conn, FirstReqired.sqlquerytext);
                    await ExecuteScriptTextAsync(conn, pg_get_coldef.sqlquerytext);
                    await ExecuteScriptTextAsync(conn, GetTableDef.sqlquerytext);
                    await ExecuteScriptTextAsync(conn, Searchlogentities.sqlquerytext);
                    await ExecuteScriptTextAsync(conn, log_ddl_changes.sqlquerytext);
                    await ExecuteScriptTextAsync(conn, Eventtrigger.sqlquerytext);

                    await ExecuteScriptTextAsync(conn, funcviewal2.sqlquerytext);


                    // diğerleri...
                }
                await ExecuteScriptTextAsync(conn, "SELECT public.funcviewgonder2('public');");// cekirge bunu schema alıp add connectionda parametril yapacak 
                                                                                               //ayrıca tip tip gonderip tableları da 100 100 gönderip offsetle her connectionın altına cizgi ile ilerleyecek
                                                                                               // ayrıca kapanısta serilize edip diske yazacak aynı zamanda son xmini yazacak bir dahaki girişde o xminden itibaren okuyacak
                var listener = new NotifyListener(connection.ConnectionString, connection);
                listener.NotifyReceived += (s, payload) =>
                {
                    // UI thread'e zorla döndür
                    if (Application.OpenForms.Count > 0)
                    {
                        var form = Application.OpenForms[0];
                        form.BeginInvoke((Action)(() =>
                        {
                            //MessageBox.Show($"[NOTIFY] {payload}", "NOTIFY", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            UpdateDataFromNotify(cont, connection, payload);
                        }));
                    }
                };

                await listener.StartAsync();
            }
        }
        private static void UpdateDataFromNotify(DdlComparatorControl cont,ConnectionItem connection, string payload)
        {
            // 02.01 Notify ile gelen payload'ı işleyip sadece ilgili obje güncellenecek

            // Basit kontrol: JSON mı değil mi?
            if (payload.StartsWith("##SHORT##")) // 02.01
            {
                var parts = payload.Substring("##SHORT##".Length).Split(":");
                if (parts.Length == 2)
                {
                    string objectType = parts[0];
                    string oid = parts[1];
                    Task.Run(async () => // 02.01 UI thread'i bloklamasın
                    {
                        try
                        {
                            using var conn = new NpgsqlConnection(connection.ConnectionString);
                            await conn.OpenAsync();
                            var cmd = new NpgsqlCommand($@"
                                SELECT objecttype, objectadi, sqltext
                                FROM funcviewtablemastercache
                                WHERE objecttype = @type AND oid::text = @oid", conn);

                            cmd.Parameters.AddWithValue("type", objectType);
                            cmd.Parameters.AddWithValue("oid", oid);

                            using var reader = await cmd.ExecuteReaderAsync();

                            if (await reader.ReadAsync())
                            {
                                string objType = reader.GetString(0);
                                string objName = reader.GetString(1);
                                string sqlText = reader.GetString(2);

                                cont.BeginInvoke((Action)(() => cont.ApplyDeltaUpdate(objType, objName, sqlText, connection)));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Notify detay sorgusunda hata: " + ex.Message);
                        }
                    });
                }
            }
            else // Tam JSON geldiyse
            {
                try
                {
                    var shortObj = System.Text.Json.JsonSerializer.Deserialize<ShortNotifyObject>(payload);
                    if (shortObj != null)
                    {
                        cont.BeginInvoke((Action)(() => cont.ApplyDeltaUpdate(
                            shortObj.type,
                            shortObj.name,
                            shortObj.sql,
                            connection)));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Notify JSON parse hatası: " + ex.Message);
                }
            }
        }
        public static async Task ExecuteScriptTextAsync(NpgsqlConnection conn, string sqlText)
        {
            using (var cmd = new NpgsqlCommand(sqlText, conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public static async Task<List<DatabaseObject>> FetchObjectsFromDatabase(DdlComparatorControl cont,ConnectionItem connection)
        {
            var objects = new List<DatabaseObject>();

            try
            {
                using (var conn = new NpgsqlConnection(connection.ConnectionString))
                {

                    await conn.OpenAsync();
                    await EnsureInitializedAsync(cont,connection, conn);
                    using (var cmd = new NpgsqlCommand("SELECT  objecttype, objectadi, sqltext FROM public.funcviewtablemastercache", conn)) //burada type alıyorum
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var dict = new Dictionary<string, DatabaseObject>();

                        while (await reader.ReadAsync())
                        {
                            string objectType = reader.GetString(0);
                            string objectName = reader.GetString(1);
                            string sqlText = reader.GetString(2);
                            if (!dict.ContainsKey(objectName))
                            {
                                dict[objectName] = new DatabaseObject
                                {
                                    ObjectType = objectType,
                                    ObjectName = objectName,
                                    ListOneDataBase = new List<OneDataBase>()
                                };
                            }

                            dict[objectName].ListOneDataBase.Add(new OneDataBase
                            {
                                SqlText = sqlText,
                                connectionItem = connection
                            });
                        }

                        objects = dict.Values.ToList();
                    }
                }
                connection.IsConnected = true;
            }
            catch (Exception ex)
            {
                connection.IsConnected = false;
                Console.WriteLine($"Veritabanından veri çekilirken hata oluştu: {ex.Message}");
            }

            return objects;
        }
        
    }
}
