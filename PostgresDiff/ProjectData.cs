using System.Collections.Generic;
namespace PostgresDiff
{
    public class ProjectData
    {
        public string ProjectName { get; set; }
        public List<LayerData> Layers { get; set; } = new List<LayerData>();
    }

    public class LayerData
    {
        public string LayerName { get; set; }  // Katman ismi
        public List<ConnectionItem> Connections { get; set; } = new List<ConnectionItem>();  // Katmana ait bağlantılar

        public List<DatabaseObject> DatabaseObjects { get; set; } = new List<DatabaseObject>(); // Karşılaştırma verileri

        public LayerData(string layerName)
        {
            LayerName = layerName;
            Connections = new List<ConnectionItem>(); // Bağlantılar başlangıçta boş
            DatabaseObjects = new List<DatabaseObject>(); // Obje karşılaştırmaları da boş
        }
        public LayerData() { }
    }

}