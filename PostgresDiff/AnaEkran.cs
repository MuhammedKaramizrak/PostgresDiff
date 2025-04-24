using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PostgresDiff
{
    public class AnaEkran : Form
    {
        private ListBox listBoxProjects;
        private FlowLayoutPanel flowLayoutPanel1;
        private List<ProjectData> _allProjects;

        public AnaEkran()
        {
            this.Text = "Postgres Diff Viewer";
            this.WindowState = FormWindowState.Maximized;

            InitializeDynamicControls();

            Load += AnaEkran_Load;
        }

        private void InitializeDynamicControls()
        {
            // SplitContainer: Sol Liste - Sağ Panel
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250
            };

            // ListBox: Project Listesi
            listBoxProjects = new ListBox
            {
                Dock = DockStyle.Fill
            };
            listBoxProjects.SelectedIndexChanged += listBoxProjects_SelectedIndexChanged;

            // FlowLayoutPanel: ProjectLayerControl'lar
            flowLayoutPanel1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.WhiteSmoke
            };

            // SplitContainer'a ekle
            splitContainer.Panel1.Controls.Add(listBoxProjects);
            splitContainer.Panel2.Controls.Add(flowLayoutPanel1);

            // Form'a ekle
            Controls.Add(splitContainer);
        }

        private void AnaEkran_Load(object sender, EventArgs e)
        {
            LoadAllProjects();
        }

        private void LoadAllProjects()
        {
            _allProjects = ProjectDataHelper.LoadAllProjectDatas();

            listBoxProjects.Items.Clear();

            foreach (var project in _allProjects)
            {
                listBoxProjects.Items.Add(project.ProjectName);
            }
        }

        private void listBoxProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxProjects.SelectedIndex >= 0)
            {
                flowLayoutPanel1.Controls.Clear();

                var selectedProject = _allProjects[listBoxProjects.SelectedIndex];
                LoadProject(selectedProject);
            }
        }

        private void LoadProject(ProjectData projectData)
        {
            foreach (var layer in projectData.Layers)
            {
                var plc = new ProjectLayerControl();
                plc.Width = 500;
                plc.Height = 300;
                plc.Margin = new Padding(10);
                plc.SetLayer(layer);
                flowLayoutPanel1.Controls.Add(plc);
            }
        }
    }
}
