using PostgresDiff;

public class ProjectLayerControl : UserControl
{
    public ProjectLayerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Name = "ProjectLayerControl";
        this.Size = new System.Drawing.Size(400, 600); // Yüksekliği artırdım
        this.BackColor = System.Drawing.Color.WhiteSmoke; // Hafif arka plan için
        this.Padding = new Padding(10);
        this.ResumeLayout(false);
    }

    public void LoadLayer(LayerData layer)
    {
        this.Controls.Clear();  // Eski içerikleri temizle

        // Katman adı en üstte
        var layerLabel = new Label
        {
            Text = layer.LayerName,
            Dock = DockStyle.Top,
            Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12, System.Drawing.FontStyle.Bold),
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Height = 30
        };
        this.Controls.Add(layerLabel);

        // Connection listesi ortada olacak
        var connectionList = new ConnectionListView(layer.LayerName)
        {
            Dock = DockStyle.Top,
            Height = 150,
            Margin = new Padding(5)
        };
        connectionList.SetConnections(layer.Connections);  // Katmanın bağlantılarını set et
        this.Controls.Add(connectionList);

        // DdlComparatorControl en altta olacak
        var ddlComparator = new DdlComparatorControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5)
        };
        ddlComparator.SetConnections(layer.Connections);  // Bağlantıları set et (şayet varsa)
        this.Controls.Add(ddlComparator);
    }
}
