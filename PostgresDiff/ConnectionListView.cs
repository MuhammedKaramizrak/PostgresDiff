using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Windows.Forms;
using Npgsql;
using PostgresDiff;
namespace PostgresDiff
{
    public class ConnectionItem
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsConnected { get; set; }
        public string Name { get; set; }
        public bool Inactive { get; set; }
        public string LogFilePath { get; set; }
        public bool IsDefault { get; set; } = false;


        public string OperatingSystem { get; set; }      // e.g. "Windows", "Linux"
        public string LogDirectory { get; set; }          // e.g. "log"
        public string LogFilePattern { get; set; }        // e.g. "postgresql-%Y-%m-%d_%H%M%S.log"
        public string DataDirectory { get; set; }         // full data directory
        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Timeout=5;";

        public override string ToString()
        {
            return $"{Name} ({Host}:{Port})";
        }
        public ConnectionItem(string name, string host, string port, string database, string username, string password, bool inactive)
        {
            Name = name;
            Host = host;
            Port = port;
            Database = database;
            Username = username;
            Password = password;
            Inactive = inactive;
            IsConnected = false; // Varsayılan bağlantı durumu
        }
    }



    public class ConnectionListView : ListView
    {
        public List<ConnectionItem> Connections { get; private set; } = new List<ConnectionItem>();

        public ConnectionListView()
        {
            InitializeComponent();
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Add Connection", null, OnAddConnection);
            contextMenu.Items.Add("Edit Connection", null, OnEditConnection);
            contextMenu.Items.Add("Delete Connection", null, OnDeleteConnection);
            contextMenu.Items.Add("Set as Default", null, OnSetAsDefault);
            this.ContextMenuStrip = contextMenu;
        }
        private void OnSetAsDefault(object sender, EventArgs e)
        {
            var selected = GetSelectedConnections().FirstOrDefault();
            if (selected == null)
            {
                MessageBox.Show("Please select a connection to mark as default.", "Default Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var conn in Connections)
                conn.IsDefault = false;

            selected.IsDefault = true;
            RefreshList();
        }

        private void OnDeleteConnection(object sender, EventArgs e)
        {
            var selected = GetSelectedConnections().FirstOrDefault();
            if (selected == null)
            {
                MessageBox.Show("Please select a connection to delete.", "Delete Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete connection \"{selected.Name}\"?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                Connections.Remove(selected);
                RefreshList();
            }
        }

        private void OnAddConnection(object sender, EventArgs e)
        {
            var form = new AddConnectionForm(this);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // AddConnectionForm zaten AddConnection(this) çağırıyor
                this.RefreshList(); // Eğer gerekirse listeyi güncelle
            }
        }

        private void OnEditConnection(object sender, EventArgs e)
        {
            var selected = GetSelectedConnections().FirstOrDefault();
            if (selected == null)
            {
                MessageBox.Show("Please select a connection to edit.", "Edit Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var form = new AddConnectionForm(this, selected);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // Seçilen bağlantı zaten güncellendi
                this.RefreshList(); // Yeniden çiz
            }
        }
      
        public void RefreshList()
        {
            this.Items.Clear();
            foreach (var conn in Connections)
            {
                var displayName = conn.IsDefault ? $"★ {conn.Name}" : conn.Name;
                var item = new ListViewItem(displayName)
                {
                    ImageKey = conn.IsConnected ? "connected" : "disconnected"
                };
                item.Font = conn.IsDefault
    ? new Font(this.Font, FontStyle.Bold)
    : this.Font;

                if (conn.Inactive)
                    item.ForeColor = Color.Gray;

                this.Items.Add(item);
            }
            Application.DoEvents();
        }



        public ConnectionListView(List<ConnectionItem> _Connections ) : this()
        {
            Connections = _Connections;
        }

        public ConnectionListView(ConnectionItem connection) : this()
        {
            if (connection != null)
            {
                AddConnection(connection);
            }
        }

        public async Task  AddConnection(List<ConnectionItem> connectionList)
        {
            if (connectionList != null)
            {
                foreach (var conn in connectionList)
                    AddConnection(conn);
            }
            Application.DoEvents();
        }

        private void InitializeComponent()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = true;
            CheckBoxes = false;
            MultiSelect = false;

            Columns.Add("Connections", 200);
            SmallImageList = new ImageList();
            SmallImageList.ImageSize = new Size(16, 16); // Gerekirse büyüt
            SmallImageList.Images.Add("connected", Properties.Resources.connected);    // eklemen gerekir
            SmallImageList.Images.Add("disconnected", Properties.Resources.disconnected);

        }

        public void AddConnection(string name, string host, string  port, string database, string username, string password, bool inactive = false)
        {
            var conn = new ConnectionItem(name, host, port, database, username, password, inactive);
            AddConnection(conn);
        }
        public async Task SetConnections(List<ConnectionItem> connections)
        {
            if (!this.IsHandleCreated)
            {
                await Task.Run(() => this.HandleCreated += async (s, e) => await SetConnections(connections));
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    RefreshList();
                }));
            }
            else
            {
                RefreshList();
            }
        }

        public void AddConnection(ConnectionItem conn)
        {
            if (conn == null)
                return;

            Connections.Add(conn);

            var displayName = conn.IsDefault ? $"★ {conn.Name}" : conn.Name;
            var item = new ListViewItem(displayName)
            {
                ImageKey = conn.IsConnected ? "connected" : "disconnected"
            };
            item.Font = conn.IsDefault
? new Font(this.Font, FontStyle.Bold)
: this.Font;

            if (conn.Inactive)
                item.ForeColor = Color.Gray;

            this.Items.Add(item);
        }

        public List<ConnectionItem> GetSelectedConnections()
        {
            var selected = new List<ConnectionItem>();
            foreach (ListViewItem item in SelectedItems)
            {
                var index = item.Index;
                if (index >= 0 && index < Connections.Count)
                    selected.Add(Connections[index]);
            }
            return selected;
        }
    }
}






