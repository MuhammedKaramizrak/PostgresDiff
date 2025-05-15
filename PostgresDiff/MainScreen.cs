using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PostgresDiff
{
    public class MainScreen : Form
    {
        private ListBox listBoxProjects;
   
        private List<ProjectData> _allProjects;

        private Button btnAddProject;
        private Button btnDeleteProject;
        private Button btnRenameProject;

        public MainScreen()
        {
            this.Text = "Postgres Diff Viewer";
            this.WindowState = FormWindowState.Maximized;

            InitializeDynamicControls();

            Load += MainScreen_Load;
            FormClosed += MainScreen_FormClosed;
        }

        private void MainScreen_FormClosed(object? sender, FormClosedEventArgs e)
        {
            SaveAllSelectedDatabases();
            ProjectDataHelper.SaveAllProjectDatas(_allProjects);
        }
        private void SaveAllSelectedDatabases()
        {
            foreach (var project in _allProjects)
            {
                foreach (var layer in project.Layers)
                {
                    foreach (Control c in splitContainer.Panel2.Controls)
                    {
                        FindAndUpdateComparatorRecursive(c, layer);
                    }
                }
            }
        }
        private void FindAndUpdateComparatorRecursive(Control parent, LayerData layer)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is DdlComparatorControl comparator)
                {
                    comparator.UpdateSelectedDatabaseInProject(layer);
                }
                else
                {
                    FindAndUpdateComparatorRecursive(child, layer);
                }
            }
        }
        private IEnumerable<Control> GetAllControlsRecursive(Control control)
        {
            foreach (Control child in control.Controls)
            {
                yield return child;
                foreach (var grandChild in GetAllControlsRecursive(child))
                {
                    yield return grandChild;
                }
            }
        }
        private SplitContainer splitContainer;

        private void InitializeDynamicControls()
        {
            // SplitContainer: Left = Project List, Right = Project Layers
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 100, // Temporary small value
                IsSplitterFixed = false,
                BorderStyle = BorderStyle.Fixed3D,
                Panel1MinSize = 30,
                Panel2MinSize = 100
            };

            // Project ListBox
            listBoxProjects = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                IntegralHeight = false,
                ItemHeight = 24
            };
            listBoxProjects.SelectedIndexChanged += listBoxProjects_SelectedIndexChanged;

            // Buttons
            btnAddProject = new Button
            {
                Text = "Add Project",
                Dock = DockStyle.Top,
                Height = 40
            };
            btnAddProject.Click += btnAddProject_Click;

            btnDeleteProject = new Button
            {
                Text = "Delete Project",
                Dock = DockStyle.Top,
                Height = 40
            };
            btnDeleteProject.Click += btnDeleteProject_Click;

            btnRenameProject = new Button
            {
                Text = "Rename Project",
                Dock = DockStyle.Top,
                Height = 40
            };
            btnRenameProject.Click += btnRenameProject_Click;

            // FlowLayoutPanel for Project Layers
            

            // Add controls to SplitContainer
            splitContainer.Panel1.Controls.Add(listBoxProjects);
            splitContainer.Panel1.Controls.Add(btnRenameProject);
            splitContainer.Panel1.Controls.Add(btnDeleteProject);
            splitContainer.Panel1.Controls.Add(btnAddProject);
           

            // Add SplitContainer to Form
            Controls.Add(splitContainer);
            
        }

        private void MainScreen_Load(object sender, EventArgs e)
        {
            LoadAllProjects();

            // Now the form has correct size, so we can adjust SplitterDistance properly
            splitContainer.SplitterDistance = this.Width / 15; // 6% of form width
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
        
        ContextMenuStrip layerMenu;
        private async void  listBoxProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxProjects.SelectedIndex >= 0)
            {
               

                var selectedProject = _allProjects[listBoxProjects.SelectedIndex];
               await LoadProject(selectedProject);
                 layerMenu = new ContextMenuStrip();
                layerMenu.Items.Add("Add Layer", null, (s, e) => AddLayer());
                layerMenu.Items.Add("Delete Last Layer", null, (s, e) => DeleteLayer());
                splitContainer.Panel2.ContextMenuStrip = layerMenu;
            }
        }
        private async void AddLayer()
        {
            if (listBoxProjects.SelectedIndex < 0) return;

            var selectedProject = _allProjects[listBoxProjects.SelectedIndex];
            string defaultName = $"Layer{selectedProject.Layers.Count + 1}";
            string layerName = Microsoft.VisualBasic.Interaction.InputBox("Enter Layer Name", "Add Layer", defaultName);

            if (string.IsNullOrWhiteSpace(layerName))
            {
                MessageBox.Show("Layer name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var newLayer = new LayerData(layerName);

            selectedProject.Layers.Add(newLayer);
            ProjectDataHelper.SaveAllProjectDatas(_allProjects);
            await LoadProject(selectedProject);
        }

        private async void DeleteLayer()
        {
            if (listBoxProjects.SelectedIndex < 0) return;

            var selectedProject = _allProjects[listBoxProjects.SelectedIndex];

            if (selectedProject.Layers.Count == 0)
            {
                MessageBox.Show("No layers to delete.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var lastLayer = selectedProject.Layers.Last();
            var confirm = MessageBox.Show(
                $"Are you sure you want to delete the last layer: \"{lastLayer.LayerName}\"?",
                "Delete Layer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                selectedProject.Layers.RemoveAt(selectedProject.Layers.Count - 1);
                ProjectDataHelper.SaveAllProjectDatas(_allProjects);
                await LoadProject(selectedProject);
            }
        }




        private async Task LoadProject(ProjectData projectData)
        {
            splitContainer.Panel2.Controls.Clear();
            SplitContainer? previousSplit = null;

            foreach (var layer in projectData.Layers)
            {
                bool isLastLayer = layer == projectData.Layers.Last();

                // Layer adı Label'ı
                var lblLayerName = new Label
                {
                    Text = layer.LayerName,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.LightSteelBlue,
                    Cursor = Cursors.Hand
                };

                lblLayerName.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        string newName = Microsoft.VisualBasic.Interaction.InputBox("Rename Layer", "Rename", layer.LayerName);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            layer.LayerName = newName;
                            lblLayerName.Text = newName;
                            ProjectDataHelper.SaveAllProjectDatas(_allProjects);
                        }
                    }
                    else if (e.Button == MouseButtons.Right && layerMenu != null)
                    {
                        layerMenu.Show(lblLayerName, e.Location);
                    }
                };

                // İçerik Paneli (Label + Conn + Comparator)
                var layerPanel = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 2,
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.FixedSingle,
                    AutoSize = false,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Margin = new Padding(10)
                };

                layerPanel.RowStyles.Clear();
                layerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Label
                layerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
                layerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                layerPanel.Controls.Add(lblLayerName, 0, 0);
                layerPanel.SetColumnSpan(lblLayerName, 2);

                // ConnectionListView
                var connView = new ConnectionListView(layer.LayerName)
                {
                    Dock = DockStyle.Fill,
                    MinimumSize = new Size(0, 100)
                };
                await connView.AddConnection(layer.Connections);
                layerPanel.Controls.Add(connView, 0, 1);

                // DdlComparatorControl
                var comparator = new DdlComparatorControl
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(5)
                };
                await comparator.SetConnectionListView(layer.Connections);
                layerPanel.Controls.Add(comparator, 1, 1);
                connView.RefreshList();

                // Eğer son layer ise yeni SplitContainer oluşturma
                if (previousSplit == null)
                {
                    if (isLastLayer)
                        splitContainer.Panel2.Controls.Add(layerPanel);
                    else
                    {
                        var newSplit = new SplitContainer
                        {
                            Orientation = Orientation.Vertical,
                            Dock = DockStyle.Fill,
                            BorderStyle = BorderStyle.FixedSingle,
                            IsSplitterFixed = false
                        };
                        newSplit.Panel1.Controls.Add(layerPanel);
                        splitContainer.Panel2.Controls.Add(newSplit);
                        previousSplit = newSplit;
                    }
                }
                else
                {
                    if (isLastLayer)
                    {
                        previousSplit.Panel2.Controls.Add(layerPanel);
                    }
                    else
                    {
                        var newSplit = new SplitContainer
                        {
                            Orientation = Orientation.Vertical,
                            Dock = DockStyle.Fill,
                            BorderStyle = BorderStyle.FixedSingle,
                            IsSplitterFixed = false
                        };
                        newSplit.Panel1.Controls.Add(layerPanel);
                        previousSplit.Panel2.Controls.Add(newSplit);
                        previousSplit = newSplit;
                    }
                }
            }
        }




        // Add Project
        private void btnAddProject_Click(object sender, EventArgs e)
        {
            string projectName = Microsoft.VisualBasic.Interaction.InputBox("Enter New Project Name", "Add Project", "", -1, -1);

            if (!string.IsNullOrEmpty(projectName))
            {
                // Create new project
                var newProject = new ProjectData { ProjectName = projectName };

                // Add new project to list
                _allProjects.Add(newProject);
                listBoxProjects.Items.Add(projectName);

                // Save project data (for example, to JSON)
                ProjectDataHelper.SaveAllProjectDatas(_allProjects);

                MessageBox.Show("New project added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Project name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Delete Project
        private void btnDeleteProject_Click(object sender, EventArgs e)
        {
            if (listBoxProjects.SelectedIndex >= 0)
            {
                var selectedProject = _allProjects[listBoxProjects.SelectedIndex];

                // Confirm delete
                var result = MessageBox.Show($"Are you sure you want to delete the project '{selectedProject.ProjectName}'?",
                                              "Delete Project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Remove from list and save
                    _allProjects.Remove(selectedProject);
                    listBoxProjects.Items.RemoveAt(listBoxProjects.SelectedIndex);

                    // Save project data
                    ProjectDataHelper.SaveAllProjectDatas(_allProjects);

                    MessageBox.Show("Project deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a project to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Rename Project
        private void btnRenameProject_Click(object sender, EventArgs e)
        {
            if (listBoxProjects.SelectedIndex >= 0)
            {
                var selectedProject = _allProjects[listBoxProjects.SelectedIndex];
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter New Project Name", "Rename Project", selectedProject.ProjectName, -1, -1);

                if (!string.IsNullOrEmpty(newName))
                {
                    selectedProject.ProjectName = newName;
                    listBoxProjects.Items[listBoxProjects.SelectedIndex] = newName;

                    // Save updated project data
                    ProjectDataHelper.SaveAllProjectDatas(_allProjects);

                    MessageBox.Show("Project renamed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Project name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a project to rename.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        
        
    }
}
