using Npgsql;
using PostgresDiff;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;
using static System.ComponentModel.Design.ObjectSelectorEditor;
namespace PostgresDiff
{
    public class DdlComparatorControl : UserControl
    {
        private DataGridView dataGridView;
        public List<ConnectionItem> Connections;
        public enum LayerType
        {
            Initial,    // İlk katman, base yok
            Middle,     // Ara katman, base var, karşılaştırma yapılabilir
            Final       // Son katman (gerekirse), yine base'e göre karşılaştırmalı
        }
        public LayerType CurrentLayerType { get; set; }

        public DdlComparatorControl()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false
            };

            dataGridView.Columns.Add("ObjectName", "Object Name");

            Controls.Add(dataGridView);
        }

        public async Task SetConnectionListView(List<ConnectionItem> Connections)
        {
            this.Connections = Connections;
            await LoadData();
        }

        private async Task LoadData()
        {
            if (Connections == null || Connections.Count == 0)
                return;

            objectData = new Dictionary<string, DatabaseObject>();
            List<Task<List<DatabaseObject>>> tasks = Connections.Select(conn => FetchObjectsFromDatabase(conn)).ToList();

            try
            {
                var results = await Task.WhenAll(tasks);

                for (int i = 0; i < results.Length; i++)
                {
                    foreach (var dbObj in results[i])
                    {
                        if (!objectData.ContainsKey(dbObj.ObjectName))
                        {
                            objectData[dbObj.ObjectName] = new DatabaseObject
                            {
                                ObjectType = dbObj.ObjectType, // ← EKLE!
                                ObjectName = dbObj.ObjectName,
                                ListOneDataBase = new List<OneDataBase>()
                            };
                        }

                        objectData[dbObj.ObjectName].ListOneDataBase.AddRange(dbObj.ListOneDataBase);
                    }
                }

                foreach (var obj in objectData.Values)
                {
                    obj.HasDifference = obj.ListOneDataBase
                        .Select(x => x.SqlText.Trim())
                        .Distinct()
                        .Count() > 1;
                    if  (!obj.HasDifference && obj.ListOneDataBase
                        .Select(x => x.SqlText.Trim()).Count() == 1)  
                        obj.AutoSelectted = true;
                }

                if (InvokeRequired)
                    Invoke(new Action(() => PopulateGrid(objectData)));
                else
                    PopulateGrid(objectData);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Hata oluştu: {e.Message}");
            }
        }
        private async Task EnsureInitializedAsync(ConnectionItem connection, NpgsqlConnection conn)
        {
            using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM pg_proc WHERE proname = 'funcviewtablemastercache'", conn))
            {
                var count = (long)(await checkCmd.ExecuteScalarAsync());
                if (count == 0 || 1==1)
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
                            UpdateDataFromNotify(connection, payload);
                        }));
                    }
                };

                await listener.StartAsync();
            }
        }
        private void UpdateDataFromNotify(ConnectionItem connection, string payload)
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

                                BeginInvoke((Action)(() => ApplyDeltaUpdate(objType, objName, sqlText, connection)));
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
                        BeginInvoke((Action)(() => ApplyDeltaUpdate(
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

        private void ApplyDeltaUpdate(string objectType, string objectName, string newSqlText, ConnectionItem sourceConn)
        {
            // 02.01: objectData içinden ilgili satırı bul ve güncelle
            if (!objectData.ContainsKey(objectName)) return;

            var dbObj = objectData[objectName];

            var one = dbObj.ListOneDataBase.FirstOrDefault(x => x.connectionItem.ConnectionString == sourceConn.ConnectionString);
            if (one != null)
            {
                one.SqlText = newSqlText; // güncelleme
            }
            else
            {
                dbObj.ListOneDataBase.Add(new OneDataBase
                {
                    SqlText = newSqlText,
                    connectionItem = sourceConn
                });
            }

            dbObj.HasDifference = dbObj.ListOneDataBase
                .Select(x => x.SqlText.Trim())
                .Distinct()
                .Count() > 1;

            if (!dbObj.HasDifference && dbObj.ListOneDataBase.Count == 1)
                dbObj.AutoSelectted = true;

            PopulateGrid(objectData); // 02.01 sadece bu satırı güncellemek için optimize edilebilir
        }

        private async Task ExecuteScriptTextAsync(NpgsqlConnection conn, string sqlText)
        {
            using (var cmd = new NpgsqlCommand(sqlText, conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<DatabaseObject>> FetchObjectsFromDatabase(ConnectionItem connection)
        {
            var objects = new List<DatabaseObject>();
           
            try
            {
                using (var conn = new NpgsqlConnection(connection.ConnectionString))
                {
                    
                    await conn.OpenAsync();
                    await EnsureInitializedAsync(connection, conn);
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

        private void SendValueMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView.Tag is int rowIndex && rowIndex >= 0)
            {
                string selectedValue = dataGridView.Rows[rowIndex].Cells[1].Value?.ToString();
                if (!string.IsNullOrEmpty(selectedValue))
                {
                    if (objectData.ContainsKey(selectedValue))
                        if (objectData.TryGetValue(selectedValue, out DatabaseObject complist))
                        {
                            var compareform = new CompareForm();
                            compareform.init2(complist);
                            compareform.ShowDialog();
                        }
                }
            }
        }
        private string RemoveBaseAndAfter(string input)
        {
            var baseIndex = input.IndexOf("Base");
            if (baseIndex >= 0)
            {
                // "Base" ve sonrasını kes
                return input.Substring(0, baseIndex);
            }
            return input; // "Base" bulunamazsa orijinal değeri döndür
        }
        private void DataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dataGridView.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0)
                {
                    dataGridView.ClearSelection();
                    dataGridView.Rows[hitTest.RowIndex].Selected = true;
                    dataGridView.Tag = hitTest.RowIndex; // Seçili satırı sakla
                }
            }
        }
        public class ShortNotifyObject
        {
            public string type { get; set; }
            public string name { get; set; }
            public string sql { get; set; }
        }
        public void SaveUserSelectedObjects()
        {
            var selectedObjects = objectData.Values
                .Where(o => o.DiffStatus == Diffstatus.RequireSelectBaseEqual && o.SelectedDatabase >= 0 && o.SelectedDatabase < o.ListOneDataBase.Count)
                .Select(o => new
                {
                    o.ObjectType,
                    o.ObjectName,
                    SelectedDatabaseName = o.ListOneDataBase[o.SelectedDatabase].connectionItem.Name,
                    SqlText = o.ListOneDataBase[o.SelectedDatabase].SqlText
                })
                .ToList();

            if (selectedObjects.Count == 0)
            {
                MessageBox.Show("No RequireSelectBaseEqual objects with user selection found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(selectedObjects, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"selected_objects_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            System.IO.File.WriteAllText(fileName, json);

            MessageBox.Show($"Saved to: {fileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        public Dictionary<string, List<string>> GetUserSelections()
        {
            var result = new Dictionary<string, List<string>>();

            if (objectData == null)
                return result;

            foreach (var kvp in objectData)
            {
                var obj = kvp.Value;

                if (obj.DiffStatus == Diffstatus.RequireSelectBaseEqual || obj.DiffStatus == Diffstatus.RequireSelectBaseDif)
                {
                    // Seçili olan database index'ini bul
                    for (int i = 0; i < Connections.Count; i++)
                    {
                        var conn = Connections[i];

                        var selected = obj.ListOneDataBase.FirstOrDefault(x => x.connectionItem.ConnectionString == conn.ConnectionString);
                        if (selected != null && obj.SelectedDatabase == i)
                        {
                            if (!result.ContainsKey(conn.Name))
                                result[conn.Name] = new List<string>();

                            result[conn.Name].Add(obj.ObjectName);
                        }
                    }
                }
            }

            return result;
        }

        public Dictionary<string, DatabaseObject> objectData;
        private void PopulateGrid(Dictionary<string, DatabaseObject> objectData2)
        {
            dataGridView.DataError += DataGridView_DataError;
            objectData = objectData2;
            dataGridView.Rows.Clear();
            dataGridView.Columns.Clear();

            // Context menu
            var contextMenu = new ContextMenuStrip();
            var sendValueMenuItem = new ToolStripMenuItem("Compare");
            sendValueMenuItem.Click += SendValueMenuItem_Click;
            contextMenu.Items.Add(sendValueMenuItem);
            dataGridView.ContextMenuStrip = contextMenu;

            // Sabit sütunlar
            dataGridView.Columns.Add("ObjectType", "Type");
            dataGridView.Columns.Add("ObjectName", "Object Name");

            // Her bağlantı için bir checkbox sütunu
            foreach (var conn in Connections)
            {
                var col = new DataGridViewCheckBoxColumn
                {
                    Name = conn.Name,
                    HeaderText = conn.Name,
                    Width = 50
                };
                dataGridView.Columns.Add(col);
            }

            // ObjectType -> DiffStatus gruplaması
            var groupedByType = objectData.Values
                .GroupBy(obj => obj.ObjectType)
                .OrderByDescending(g => g.Any(x => x.HasDifference))
                .ThenBy(g => g.Key);

            foreach (var group in groupedByType)
            {
                // Ana grup başlığı (ObjectType)
                var headerRow = new DataGridViewRow
                {
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = Color.LightGray,
                        Font = new Font(dataGridView.Font, FontStyle.Bold)
                    },
                    Tag = $"GroupHeader|{group.Key}|Expanded"
                };

                var headerCell = new DataGridViewTextBoxCell { Value = "[-] " + group.Key };
                headerRow.Cells.Add(headerCell);

                for (int i = 1; i < dataGridView.Columns.Count; i++)
                {
                    DataGridViewCell cell;
                    if (dataGridView.Columns[i] is DataGridViewCheckBoxColumn)
                    {
                        cell = new DataGridViewCheckBoxCell();
                        headerRow.Cells.Add(cell);
                        cell.Value = false;
                        cell.ReadOnly = true;
                        cell.Style.BackColor = Color.WhiteSmoke;
                    }
                    else
                    {
                        cell = new DataGridViewTextBoxCell();
                        headerRow.Cells.Add(cell);
                    }
                }

                dataGridView.Rows.Add(headerRow);

                // Alt grup: DiffStatus
                var groupedByDiffStatus = group
                    .GroupBy(x => x.DiffStatus)
                    .OrderBy(x => x.Key); // Equal, AutoSelect, RequireSelect

                foreach (var diffGroup in groupedByDiffStatus)
                {
                    // Alt grup başlığı (DiffStatus)
                    var subHeaderRow = new DataGridViewRow
                    {
                        DefaultCellStyle = new DataGridViewCellStyle
                        {
                            BackColor = Color.WhiteSmoke,
                            Font = new Font(dataGridView.Font, FontStyle.Italic)
                        },
                        Tag = $"SubGroupHeader|{group.Key}|{diffGroup.Key}|{((diffGroup.Key != Diffstatus.EqualBaseEqual 
                        && diffGroup.Key != Diffstatus.AutoSelectBaseEqual) ? "Expanded" : "Collapsed")}"

                };

                    var subHeaderCell = new DataGridViewTextBoxCell { Value = ((diffGroup.Key != Diffstatus.EqualBaseEqual
                        && diffGroup.Key != Diffstatus.AutoSelectBaseEqual) ? "[-] ":"[+] ") + ((CurrentLayerType == LayerType.Initial)
        ? RemoveBaseAndAfter(diffGroup.Key.ToString()): diffGroup.Key.ToString()) };
                    subHeaderRow.Cells.Add(subHeaderCell);

                    for (int i = 1; i < dataGridView.Columns.Count; i++)
                    {
                        DataGridViewCell cell;
                        if (dataGridView.Columns[i] is DataGridViewCheckBoxColumn)
                        {
                            cell = new DataGridViewCheckBoxCell();
                            subHeaderRow.Cells.Add(cell);
                            cell.Value = false;
                            cell.ReadOnly = true;
                            cell.Style.BackColor = Color.WhiteSmoke;
                        }
                        else
                        {
                            cell = new DataGridViewTextBoxCell();
                            subHeaderRow.Cells.Add(cell);
                        }
                    }

                    dataGridView.Rows.Add(subHeaderRow);

                    // Objeleri sırala ve ekle
                    var orderedObjects = diffGroup.OrderBy(x => x.ObjectName);

                    foreach (var dbObj in orderedObjects)
                    {
                        var row = new DataGridViewRow();
                        row.Cells.Add(new DataGridViewTextBoxCell { Value = dbObj.ObjectType });
                        row.Cells.Add(new DataGridViewTextBoxCell { Value = dbObj.ObjectName });

                        int foundCount = 0;

                        foreach (var conn in Connections)
                        {
                            var oneDb = dbObj.ListOneDataBase
                                .FirstOrDefault(x => x.connectionItem.ConnectionString == conn.ConnectionString);

                            var cbCell = new DataGridViewCheckBoxCell();
                            row.Cells.Add(cbCell);
                            cbCell.Value = false;

                            if (oneDb != null)
                            {
                                foundCount++;
                                if (dbObj.HasDifference)
                                    cbCell.Style.BackColor = Color.Yellow;
                            }
                            else
                            {
                                cbCell.ReadOnly = true;
                                cbCell.Style.BackColor = Color.LightGray;
                            }
                        }

                        if (foundCount == 1)
                        {
                            dbObj.AutoSelectted = true;
                            for (int i = 2; i < row.Cells.Count; i++)
                            {
                                if (row.Cells[i] is DataGridViewCheckBoxCell cb && !cb.ReadOnly)
                                {
                                    cb.Value = true;
                                    break;
                                }
                            }
                        }

                        row.Tag = $"DataRow|{group.Key}|{diffGroup.Key}";
                        dataGridView.Rows.Add(row);
                        row.Visible = (diffGroup.Key != Diffstatus.EqualBaseEqual
                        && diffGroup.Key != Diffstatus.AutoSelectBaseEqual);
                    }
                }
            }

            dataGridView.CellContentClick -= DataGridView_CellContentClick;
            dataGridView.CellContentClick += DataGridView_CellContentClick;

            dataGridView.MouseDown -= DataGridView_MouseDown;
            dataGridView.MouseDown += DataGridView_MouseDown;

            dataGridView.CellClick -= dataGridView_CellClick;
            dataGridView.CellClick += dataGridView_CellClick;
        }


        private void dataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var clickedRow = dataGridView.Rows[e.RowIndex];
            var tagParts = clickedRow.Tag?.ToString()?.Split('|');
            if (tagParts == null || tagParts.Length == 0) return;

            bool isGroupHeader = tagParts[0] == "GroupHeader";
            bool isSubGroupHeader = tagParts[0] == "SubGroupHeader";

            if (isGroupHeader)
            {
                string groupKey = tagParts.Length > 1 ? tagParts[1] : "";

                bool anyVisible = false;

                for (int i = e.RowIndex + 1; i < dataGridView.Rows.Count; i++)
                {
                    var row = dataGridView.Rows[i];
                    var rowTag = row.Tag?.ToString();
                    if (rowTag != null && rowTag.StartsWith("GroupHeader")) break;

                    if (row.Visible) anyVisible = true;
                }

                for (int i = e.RowIndex + 1; i < dataGridView.Rows.Count; i++)
                {
                    var row = dataGridView.Rows[i];
                    var rowTag = row.Tag?.ToString();
                    if (rowTag != null && rowTag.StartsWith("GroupHeader")) break;

                    row.Visible = !anyVisible;
                }

                clickedRow.Cells[0].Value = anyVisible ? "[+]" + " " + groupKey : "[-]" + " " + groupKey;
                Application.DoEvents();
            }
            else if (isSubGroupHeader)
            {
                string groupKey = tagParts.Length > 1 ? tagParts[1] : "";
                string diffStatusKey = tagParts.Length > 2 ? tagParts[2] : "";

                int startIndex = e.RowIndex;
                int endIndex = dataGridView.Rows.Count;

                for (int i = startIndex + 1; i < dataGridView.Rows.Count; i++)
                {
                    var rowTag = dataGridView.Rows[i].Tag?.ToString();
                    if (rowTag != null && (rowTag.StartsWith("GroupHeader") || rowTag.StartsWith("SubGroupHeader")))
                    {
                        endIndex = i;
                        break;
                    }
                }

                bool anyVisible = false;
                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    if (dataGridView.Rows[i].Visible)
                    {
                        anyVisible = true;
                        break;
                    }
                }

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    dataGridView.Rows[i].Visible = !anyVisible;
                }

                clickedRow.Cells[0].Value = anyVisible ? "[+]" + " " + diffStatusKey : "[-]" + " " + diffStatusKey;
                Application.DoEvents();
            }
        }

        












        private void DataGridView_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            MessageBox.Show(e.ToString());
        }

        public void UpdateSelectedDatabaseInProject(LayerData layer)
        {
            if (objectData == null)
                return;

            layer.DatabaseObjects.Clear(); // Eski seçimleri sıfırla

            foreach (var kvp in objectData)
            {
                var dbObj = kvp.Value;

                // Sadece seçim yapılmışsa ekle
                if (dbObj.SelectedDatabase > 0)
                {
                    layer.DatabaseObjects.Add(new DatabaseObject
                    {
                        ObjectName = dbObj.ObjectName,
                        SelectedDatabase = dbObj.SelectedDatabase
                    });
                }
            }
        }




        // Bu metot, kullanıcı bir checkbox'ı tıkladığında çalışacak
        private void DataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (dataGridView.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
                {
                    var cell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    if (cell.ReadOnly) return;

                    // Mevcut değer tersine çevrilir (toggle)
                    bool isSelected = cell.Value != null && (bool)cell.Value;
                    cell.Value = !isSelected;

                    string objectName = dataGridView.Rows[e.RowIndex].Cells[1].Value?.ToString();
                    if (string.IsNullOrEmpty(objectName)) return;

                    if (objectData.TryGetValue(objectName, out var dbObj))
                    {
                        if ((bool)cell.Value == true)
                            dbObj.SelectedDatabase = e.ColumnIndex - 1; // çünkü checkbox'lar 2. kolondan başlıyor → DB1 = 2-1 = 1
                        else
                            dbObj.SelectedDatabase = 0;
                    }

                    // Aynı satırda diğer tüm checkbox'ları sıfırla
                    for (int i = 2; i < dataGridView.Columns.Count; i++)
                    {
                        if (i != e.ColumnIndex &&
                            dataGridView.Columns[i] is DataGridViewCheckBoxColumn &&
                            dataGridView.Rows[e.RowIndex].Cells[i] is DataGridViewCheckBoxCell otherCell &&
                            !otherCell.ReadOnly)
                        {
                            otherCell.Value = false;
                        }
                    }
                }
            }
        }




    }

    public enum Diffstatus
    {
        EqualBaseEqual,            // Tam eşleşme
        AutoSelectBaseEqual,       // Otomatik seçilmiş ama SQL farkı yok
        RequireSelectBaseEqual,    // Fark var ama Base ile aynı

        EqualBaseDif,              // DB içi fark yok ama base'e göre fark var
        AutoSelectBaseDif,         // Otomatik seçim ve base farkı var
        RequireSelectBaseDif       // DB içi fark ve base farkı da var
            
    }

    public class DatabaseObject
    {
        public DatabaseObject()
        {
                
        }
        public string ObjectType { get; set; }
        public string ObjectName { get; set; }

        public List<OneDataBase> ListOneDataBase { get; set; }

        public int SelectedDatabase 
        { get; 
            set; 
        }
        public bool AutoSelectted { get; set; }
        public bool HasDifference { get; set; }

        public string BaseSqlText { get; set; } // Yeni eklenen alan

        public bool BaseDifference { get; set; } // Base ile fark var mı?

        public Diffstatus DiffStatus
        {
            get
            {
                if (!HasDifference && !BaseDifference)
                {
                    // Tam eşleşme ve base ile de fark yoksa
                    return AutoSelectted ? Diffstatus.AutoSelectBaseEqual : Diffstatus.EqualBaseEqual;
                }
                else if (HasDifference && !BaseDifference)
                {
                    // DB içi fark var ama base ile fark yok
                    return AutoSelectted ? Diffstatus.AutoSelectBaseDif : Diffstatus.RequireSelectBaseDif;
                }
                else if (!HasDifference && BaseDifference)
                {
                    // Base ile fark var ama DB içi fark yok
                    return AutoSelectted ? Diffstatus.AutoSelectBaseEqual : Diffstatus.EqualBaseDif;
                }
                else
                {
                    // Hem DB içi fark var hem de base ile fark var
                    return AutoSelectted ? Diffstatus.AutoSelectBaseDif : Diffstatus.RequireSelectBaseDif;
                }
            }
           
        }
    }


    public class OneDataBase
    {
        public string SqlText { get; set; }
        public ConnectionItem connectionItem { get; set; }

    }
}

