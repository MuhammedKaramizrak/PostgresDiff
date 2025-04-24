using System.Collections.Generic;
namespace PostgresDiff
{
    public class ProjectData
    {
        public List<LayerData> Layers { get; set; } = new List<LayerData>();
    }

    public class LayerData
    {
        public string LayerName { get; set; }  // Katman ismi
        public List<ConnectionItem> Connections { get; set; } = new List<ConnectionItem>();  // Katmana ait bağlantılar
        public LayerData(string layerName)
        {
            LayerName = layerName;
            Connections = new List<ConnectionItem>(); // Bağlantılar başlangıçta boş
        }
    }
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

        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Timeout=5;";

        public override string ToString()
        {
            return $"{Name} ({Host}:{Port})";
        }
    }
}