
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace PostgresDiff
{
    public class ConnectionListView : ListView
    {
        private ImageList _imageList;
        public List<ConnectionItem> _connections;
        private ContextMenuStrip contextMenu;

        public ConnectionListView(string masorclient)
        {
            Init(masorclient);
        }

        public ConnectionListView()
        {
            Init("");
        }

        private void Init(string masorclient)
        {
            this.View = View.Details;
            this.FullRowSelect = true;
            this.Columns.Add(masorclient + " Connection", 300);

            _imageList = new ImageList();
            _imageList.Images.Add("disconnected", SystemIcons.Error.ToBitmap());
            _imageList.Images.Add("connected", SystemIcons.Information.ToBitmap());
            _imageList.Images.Add("log_trigger", SystemIcons.Warning.ToBitmap());
            _imageList.Images.Add("no_log_trigger", SystemIcons.Question.ToBitmap());

            this.SmallImageList = _imageList;
            _connections = new List<ConnectionItem>();

            contextMenu = new ContextMenuStrip();

            ToolStripMenuItem editMenuItem = new ToolStripMenuItem("Edit Connection");
            editMenuItem.Click += EditMenuItem_Click;
            contextMenu.Items.Add(editMenuItem);

            ToolStripMenuItem fixTriggerMenuItem = new ToolStripMenuItem("Fix Event Trigger");
            fixTriggerMenuItem.Click += FixTriggerMenuItem_Click;
            contextMenu.Items.Add(fixTriggerMenuItem);

            ToolStripMenuItem dropTriggerMenuItem = new ToolStripMenuItem("Drop Event Trigger");
            dropTriggerMenuItem.Click += DropTriggerMenuItem_Click;
            contextMenu.Items.Add(dropTriggerMenuItem);

            this.ContextMenuStrip = contextMenu;
        }

        private async Task DropLogTriggerIfExists(NpgsqlConnection conn)
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            try
            {
                using (var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_event_trigger WHERE evtname = 'track_ddl_changes');", conn))
                {
                    bool exists = (bool)(await cmd.ExecuteScalarAsync() ?? false);
                    if (exists)
                    {
                        using (var dropCmd = new NpgsqlCommand(@"
                            DROP EVENT TRIGGER IF EXISTS track_ddl_changes;
                            DROP FUNCTION IF EXISTS log_ddl_changes CASCADE;", conn))
                        {
                            await dropCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error dropping trigger: {ex.Message}");
            }
        }

        private async void DropTriggerMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a connection first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int index = this.SelectedItems[0].Index;
            if (index < 0 || index >= _connections.Count) return;

            var connection = _connections[index];

            try
            {
                using (var conn = new NpgsqlConnection(connection.ConnectionString))
                {
                    await conn.OpenAsync();
                    await DropLogTriggerIfExists(conn);
                    MessageBox.Show("Event Trigger has been dropped.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await CheckConnectionStatus(index);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void FixTriggerMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a connection first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int index = this.SelectedItems[0].Index;
            if (index < 0 || index >= _connections.Count) return;

            var connection = _connections[index];

            try
            {
                using (var conn = new NpgsqlConnection(connection.ConnectionString))
                {
                    await conn.OpenAsync();
                    await EnsureLogTriggerExists(conn);
                    MessageBox.Show("Event Trigger checked and updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await CheckConnectionStatus(index);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task EnsureLogTriggerExists(NpgsqlConnection conn)
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            try
            {
                string sqltext = new log_ddl_changes().sqlquerytext;

                using (var checkCmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_event_trigger WHERE evtname = 'track_ddl_changes');", conn))
                {
                    bool exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

                    if (!exists)
                    {
                        using (var createCmd = new NpgsqlCommand(sqltext, conn))
                        {
                            await createCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        using (var updateCmd = new NpgsqlCommand(@"
                            DROP EVENT TRIGGER IF EXISTS track_ddl_changes;
                            DROP FUNCTION IF EXISTS log_ddl_changes();

                            CREATE OR REPLACE FUNCTION log_ddl_changes()
                            RETURNS event_trigger AS $$
                            BEGIN
                                INSERT INTO ddl_log (event_time, command_tag, object_schema, object_identity, user_name)
                                SELECT now(), command_tag, schema_name, object_identity, current_user
                                FROM pg_event_trigger_ddl_commands();
                            END;
                            $$ LANGUAGE plpgsql;

                            CREATE EVENT TRIGGER track_ddl_changes
                            ON ddl_command_end
                            EXECUTE FUNCTION log_ddl_changes();", conn))
                        {
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring trigger exists: {ex.Message}");
            }
        }

        private void EditMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a connection to edit.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int index = this.SelectedItems[0].Index;
            if (index >= 0 && index < _connections.Count)
            {
                ConnectionItem selectedConnection = _connections[index];
                AddConnectionForm editForm = new AddConnectionForm(this, selectedConnection);
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    this.Items[index].Text = selectedConnection.ToString();
                    CheckConnectionStatus(index);
                }
            }
        }

        public void AddConnection(string name, string host, string port, string database, string username, string password, bool inactive)
        {
            ConnectionItem connection = new ConnectionItem
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password,
                IsConnected = false,
                Name = name,
                Inactive = inactive
            };

            _connections.Add(connection);

            ListViewItem item = new ListViewItem(connection.ToString())
            {
                ImageKey = "disconnected"
            };

            if (inactive)
                item.ForeColor = Color.Gray;

            this.Items.Add(item);

            if (!inactive)
            {
                CheckConnectionStatus(this.Items.Count - 1);
            }
        }

        public void RemoveSelectedConnection()
        {
            if (this.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a connection to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int index = this.SelectedItems[0].Index;
            if (index >= 0 && index < _connections.Count)
            {
                _connections.RemoveAt(index);
                this.Items.RemoveAt(index);
            }
        }

        private async Task<bool> CheckLogTriggerExists(NpgsqlConnection conn)
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            try
            {
                using (var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_event_trigger WHERE evtname = 'track_ddl_changes');", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    return result != DBNull.Value && (bool)result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking trigger: {ex.Message}");
                return false;
            }
        }

        public async Task CheckConnectionStatus(int index)
        {
            await Task.Delay(0).ConfigureAwait(false);
            if (index < 0 || index >= _connections.Count) return;

            var connection = _connections[index];
            try
            {
                using (var conn = new NpgsqlConnection(connection.ConnectionString))
                {
                    await conn.OpenAsync();
                    connection.IsConnected = true;
                    bool hasTrigger = await CheckLogTriggerExists(conn);

                    this.Invoke((MethodInvoker)(() =>
                    {
                        connection.IsConnected = true;
                        this.Items[index].ImageKey = hasTrigger ? "log_trigger" : "no_log_trigger";
                    }));
                }
            }
            catch
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    connection.IsConnected = false;
                    this.Items[index].ImageKey = "disconnected";
                }));
            }
        }

        public List<ConnectionItem> GetConnections()
        {
            return _connections;
        }

        public async Task SetConnections(List<ConnectionItem> connections)
        {
            this.Invoke((MethodInvoker)(() =>
            {
                this.Items.Clear();
                _connections.Clear();
            }));

            var tasks = new List<Task>();

            foreach (var connection in connections)
            {
                int index = -1;

                this.Invoke((MethodInvoker)(() =>
                {
                    _connections.Add(connection);
                    var item = new ListViewItem(connection.ToString())
                    {
                        ImageKey = "disconnected"
                    };
                    if (connection.Inactive)
                        item.ForeColor = Color.Gray;

                    this.Items.Add(item);
                    index = this.Items.Count - 1;
                }));

                if (!connection.Inactive && index >= 0)
                {
                    tasks.Add(CheckConnectionStatus(index));
                }
            }

            await Task.WhenAll(tasks);
        }
    }
}
