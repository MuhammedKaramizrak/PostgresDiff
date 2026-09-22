
using System.Security.AccessControl;

namespace PostgresDiff
{
    public class LayerBaseBuilder
    {
        private readonly ProjectData project;

        public LayerBaseBuilder(ProjectData project)
        {
            this.project = project;
        }

        // Tüm liste için base oluştur ve katmana enjekte et
        public void InjectBaseFromPreviousLayer(int fromLayerIndex)
        {
            if (fromLayerIndex < 0 || fromLayerIndex >= project.Layers.Count - 1) return;

            var baseLayer = project.Layers[fromLayerIndex];
            var targetLayer = project.Layers[fromLayerIndex + 1];

            // Eğer hedef katman boşsa obje başlıklarını kopyala
            if (targetLayer.DatabaseObjects.Count == 0)
            {
                foreach (var baseObj in baseLayer.DatabaseObjects)
                {
                    targetLayer.DatabaseObjects.Add(new DatabaseObject
                    {
                        ObjectType = baseObj.ObjectType,
                        ObjectName = baseObj.ObjectName,
                        ListOneDataBase = new List<OneDataBase>()
                    });
                }
            }

            foreach (var baseObj in baseLayer.DatabaseObjects)
            {
                // targetObj yoksa oluştur
                var targetObj = targetLayer.DatabaseObjects
                    .FirstOrDefault(x => x.ObjectName == baseObj.ObjectName && x.ObjectType == baseObj.ObjectType);

                if (targetObj == null)
                {
                    targetObj = new DatabaseObject
                    {
                        ObjectType = baseObj.ObjectType,
                        ObjectName = baseObj.ObjectName,
                        ListOneDataBase = new List<OneDataBase>()
                    };
                    targetLayer.DatabaseObjects.Add(targetObj);
                }

                InjectSingleBase(baseObj, targetLayer);

                AutoSelectIfRequired(targetObj);
            }
        }


        // Tek obje için base enjekte et
        public void InjectSingleBase(DatabaseObject baseObj, LayerData targetLayer)
        {
            var selected = baseObj.GetSelectedOneDatabase()
                          ?? baseObj.ListOneDataBase.FirstOrDefault(x => x.connectionItem.IsDefault)
                          ?? baseObj.ListOneDataBase.FirstOrDefault();

            if (selected == null) return;

            var targetObj = targetLayer.DatabaseObjects
                .FirstOrDefault(x => x.ObjectName == baseObj.ObjectName && x.ObjectType == baseObj.ObjectType);

            if (targetObj == null) return;

            var baseDb = new OneDataBase
            {
                SqlText = selected.SqlText,
                connectionItem = new ConnectionItem()
                {
                    Name = "base",
                    IsVirtual = true
                }
            };

            // Eğer daha önce eklenmişse güncelle
            var existingBase = targetObj.ListOneDataBase
                .FirstOrDefault(x => x.connectionItem.IsVirtual && x.connectionItem.Name == "base");

            if (existingBase != null)
            {
                existingBase.SqlText = baseDb.SqlText;
            }
            else
            {
                targetObj.ListOneDataBase.Insert(0, baseDb);
            }

            targetObj.BaseSqlText = baseDb.SqlText;
            UpdateDifferenceFlags(targetObj);
        }
        // Eğer RequireSelectBaseEqual ise ve seçili yoksa otomatik seçim yapar
        public void AutoSelectIfRequired(DatabaseObject obj)
        {
            if (obj.SelectedDatabase >= 0) return; // Zaten seçim yapılmış

            if (obj.DiffStatus == Diffstatus.RequireSelectBaseEqual)
            {
                // 1. Default bağlantıyı bul
                int defaultIndex = obj.ListOneDataBase
                    .FindIndex(x => !x.connectionItem.IsVirtual && x.connectionItem.IsDefault);

                if (defaultIndex >= 0)
                {
                    obj.SelectedDatabase = defaultIndex;
                    obj.AutoSelectted = true;
                    return;
                }

                // 2. Sanal "base" bağlantıyı bul
                int baseIndex = obj.ListOneDataBase
                    .FindIndex(x => x.connectionItem.IsVirtual && x.connectionItem.Name == "base");

                if (baseIndex >= 0)
                {
                    obj.SelectedDatabase = baseIndex;
                    obj.AutoSelectted = true;
                    return;
                }

                // 3. Hiçbiri yoksa dokunma
            }
        }

        private void UpdateDifferenceFlags(DatabaseObject targetObj)
        {
            var selected = targetObj.GetSelectedOneDatabase();
            if (selected == null) return;

            // DB içi fark var mı?
            targetObj.HasDifference = targetObj.ListOneDataBase
                .Where(x => !x.connectionItem.IsVirtual)
                .Select(x => x.SqlText?.Trim())
                .Distinct()
                .Count() > 1;

            // Base ile fark var mı?
            var baseSql = targetObj.BaseSqlText?.Trim();
            var selectedSql = selected.SqlText?.Trim();
            targetObj.BaseDifference = baseSql != selectedSql;
        }
    }
}