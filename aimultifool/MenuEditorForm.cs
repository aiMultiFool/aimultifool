using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Runtime.InteropServices;


namespace aimultifool
{

    public partial class MenuEditorForm : Form
    {
        private TreeView treeViewMenus;
        private TextBox txtSectionName, txtItemName, txtPrompt;
        private Button btnAddSection, btnAddItem, btnRemove, btnSaveChanges;
        private JObject menuJson;
        public event Action OnMenusSaved;
        private Button btnAddSeparator, btnExportSection, btnImportSection, btnExportAll, btnDuplicateItem;
        private JObject settingsJson;
        private bool isProgrammaticUpdate = false;


        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // Add the list of restricted section names
        private readonly HashSet<string> restrictedSectionNames = new HashSet<string>
{
    "Files",
    "Tools",
    "Options",
    "Help",
    "Modifiers"
};
        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }
        public MenuEditorForm()
        {
            EnableDarkMode(this.Handle); // Apply dark mode

            InitializeComponents();
            LoadSettings();
            LoadJson();
            PopulateTreeView();

            // Make the form non-resizable
            this.FormBorderStyle = FormBorderStyle.Sizable;
            //this.MaximizeBox = false;
            //this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            //this.ShowIcon = false;
            //this.TopMost = true; // Ensures the form stays on top
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Icon = aimultifool.Resource.aimultifool;
        }

        // Real-time update of Item Name
        private void TxtItemName_TextChanged(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedObject &&
                !selectedObject.ContainsKey("items")) // Ensure it's an item
            {
                selectedObject["itemName"] = txtItemName.Text;
                treeViewMenus.SelectedNode.Text = txtItemName.Text; // Update the TreeView node text
            }
        }
        private void InitializeComponents()
        {
            // Form settings
            this.Text = "Action Menu Editor";
            this.Width = 680;
            this.Height = 440;

            // Label for drag-and-drop instructions
            Label lblInstructions = new Label
            {
                Text = "Drag and drop items to move them",
                Location = new System.Drawing.Point(10, 10),
                Width = 300,
                AutoSize = true
            };
            this.Controls.Add(lblInstructions);

            // TreeView
            treeViewMenus = new TreeView
            {
                Location = new System.Drawing.Point(10, 30), // Positioned below the label
                Size = new System.Drawing.Size(300, 360),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left // Adjusts height and width
            };
            treeViewMenus.AfterSelect += TreeViewMenus_AfterSelect;
            this.Controls.Add(treeViewMenus);
            // Section Name TextBox
            txtSectionName = new TextBox
            {
                Location = new System.Drawing.Point(320, 30),
                Width = 200,
                //Multiline = true,
                PlaceholderText = "Menu Name",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, // Adjust width
                //ScrollBars = ScrollBars.Vertical
            };
            txtSectionName.TextChanged += TxtSectionName_TextChanged; // Attach TextChanged event
            //txtSectionName.TextChanged += AdjustHeightToContent;
            this.Controls.Add(txtSectionName);

            // Item Name TextBox
            txtItemName = new TextBox
            {
                Location = new System.Drawing.Point(320, 60),
                Width = 200,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, // Adjust width
                //Multiline = true,
                PlaceholderText = "Item Name",
                //ScrollBars = ScrollBars.Vertical
            };
            txtItemName.TextChanged += TxtItemName_TextChanged; // Attach TextChanged event
            //txtItemName.TextChanged += AdjustHeightToContent;
            this.Controls.Add(txtItemName);

            txtPrompt = new TextBox
            {
                Location = new System.Drawing.Point(320, 90),
                Width = 200,
                Multiline = true,
                PlaceholderText = "Prompt",
                ScrollBars = ScrollBars.Vertical,
                Height = 300,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, // Adjust width and height
            };
            txtPrompt.TextChanged += TxtPrompt_TextChanged; // Attach the TextChanged event
            //txtPrompt.TextChanged += AdjustHeightToContent; // Adjust height dynamically
            this.Controls.Add(txtPrompt);


            // Buttons container (Panel)
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 140 // Adjust width of the panel
            };
            this.Controls.Add(buttonPanel);

            // Buttons in the panel
            btnAddSection = CreateButton("Add Menu", 4, 30);
            btnAddSection.Click += BtnAddSection_Click;
            buttonPanel.Controls.Add(btnAddSection);

            btnAddItem = CreateButton("Add Item", 4, 70);
            btnAddItem.Click += BtnAddItem_Click;
            buttonPanel.Controls.Add(btnAddItem);

            btnAddSeparator = CreateButton("Add Separator", 4, 110);
            btnAddSeparator.Click += BtnAddSeparator_Click;
            buttonPanel.Controls.Add(btnAddSeparator);

            btnDuplicateItem = CreateButton("Duplicate Item", 4, 150);
            btnDuplicateItem.Click += BtnDuplicateItem_Click;
            buttonPanel.Controls.Add(btnDuplicateItem);

            btnRemove = CreateButton("Remove Selected", 4, 190);
            btnRemove.Click += BtnRemove_Click;
            buttonPanel.Controls.Add(btnRemove);

            btnExportSection = CreateButton("Export Menu", 4, 230);
            btnExportSection.Click += BtnExportSection_Click;
            buttonPanel.Controls.Add(btnExportSection);

            btnExportAll = CreateButton("Export All", 4, 270);
            btnExportAll.Click += BtnExportAll_Click;
            buttonPanel.Controls.Add(btnExportAll);

            btnImportSection = CreateButton("Import", 4, 310);
            btnImportSection.Click += BtnImportSection_Click;
            buttonPanel.Controls.Add(btnImportSection);

            btnSaveChanges = CreateButton("Apply Changes", 4, 350);
            btnSaveChanges.Click += BtnSaveChanges_Click;
            buttonPanel.Controls.Add(btnSaveChanges);


            treeViewMenus.AllowDrop = true;

            // Add drag-and-drop event handlers
            treeViewMenus.ItemDrag += TreeViewMenus_ItemDrag;
            treeViewMenus.DragEnter += TreeViewMenus_DragEnter;
            treeViewMenus.DragOver += TreeViewMenus_DragOver;
            treeViewMenus.DragDrop += TreeViewMenus_DragDrop;
        }

        // Helper to create buttons
        private Button CreateButton(string text, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Width = 125,
                Height = 30
            };
        }

        private void BtnDuplicateItem_Click(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedItem &&
                !(selectedItem.ContainsKey("items"))) // Ensure it's an item, not a section
            {
                // Duplicate the item by creating a copy of the selected JSON object
                JObject duplicateItem = new JObject(selectedItem);

                // Modify itemName to indicate duplication
                duplicateItem["itemName"] = $"{duplicateItem["itemName"]} (Copy)";

                // Add the duplicated item to the parent section
                if (treeViewMenus.SelectedNode.Parent?.Tag is JObject parentSection &&
                    parentSection.ContainsKey("items"))
                {
                    JArray items = (JArray)parentSection["items"];
                    items.Add(duplicateItem);

                    // Refresh the TreeView to reflect changes
                    PopulateTreeView();
                }
            }
            else
            {
                CustomMessageBox.Show("Please select a valid item to duplicate.", "Error", this, isOkOnly: true);

            }
        }


        private void BtnExportSection_Click(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedSection &&
                selectedSection.ContainsKey("items"))
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "JSON Files (*.json)|*.json";
                    saveFileDialog.DefaultExt = "json";
                    saveFileDialog.Title = "Export Menu";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(saveFileDialog.FileName, selectedSection.ToString(Formatting.Indented));
                        CustomMessageBox.Show("Menu exported successfully!", "Success", this, isOkOnly: true);

                    }
                }
            }
            else
            {
                CustomMessageBox.Show("Please select a valid Menu to export.", "Error", this, isOkOnly: true);

            }
        }

        private void BtnImportSection_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON Files (*.json)|*.json";
                openFileDialog.Title = "Import Section(s)";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string importedJson = File.ReadAllText(openFileDialog.FileName);
                        JToken importedData = JToken.Parse(importedJson);

                        // Check if the imported JSON is a single section or multiple sections
                        if (importedData is JObject singleSection &&
                            singleSection.ContainsKey("sectionName") &&
                            singleSection.ContainsKey("items"))
                        {
                            // Single section case
                            JArray sections = (JArray)menuJson["ui"];
                            sections.Add(singleSection);
                            CustomMessageBox.Show("Menu imported successfully!", "Success", this, isOkOnly: true);

                        }
                        else if (importedData is JArray multipleSections)
                        {
                            // Multiple sections case
                            JArray sections = (JArray)menuJson["ui"];
                            foreach (JToken section in multipleSections)
                            {
                                if (section is JObject sectionObject &&
                                    sectionObject.ContainsKey("sectionName") &&
                                    sectionObject.ContainsKey("items"))
                                {
                                    sections.Add(sectionObject);
                                }
                            }
                            CustomMessageBox.Show("Menu imported successfully!", "Success", this, isOkOnly: true);

                        }
                        else
                        {
                            CustomMessageBox.Show("The imported JSON does not contain valid Menu(s).", "Error", this, isOkOnly: true);

                        }

                        PopulateTreeView();
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Error importing Menu(s): {ex.Message}", "Error", this, isOkOnly: true);

                    }
                }
            }
        }


        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON Files (*.json)|*.json";
                saveFileDialog.DefaultExt = "json";
                saveFileDialog.Title = "Export All Menus";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Extract the array of sections directly
                        JArray sections = (JArray)menuJson["ui"];
                        File.WriteAllText(saveFileDialog.FileName, sections.ToString(Formatting.Indented));
                        CustomMessageBox.Show("All Menus exported successfully!", "Success", this, isOkOnly: true);

                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Error exporting all Menus: {ex.Message}", "Error", this, isOkOnly: true);

                    }
                }
            }
        }



        // Add this new event handler for the button click
        private void BtnAddSeparator_Click(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedSection &&
                selectedSection.ContainsKey("items"))
            {
                JObject separatorItem = new JObject
                {
                    ["itemName"] = "-",
                    ["prompt"] = "-"
                };

                JArray items = (JArray)selectedSection["items"];
                items.Add(separatorItem);

                PopulateTreeView();
            }
            else
            {
                CustomMessageBox.Show("Please select a Menu to add a separator.", "Error", this, isOkOnly: true);

            }
        }


        // Start dragging the selected node
        private void TreeViewMenus_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        // Handle drag enter to allow drop
        private void TreeViewMenus_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        // Handle drag over to show visual feedback
        private void TreeViewMenus_DragOver(object sender, DragEventArgs e)
        {
            TreeView treeView = sender as TreeView;
            Point targetPoint = treeView.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeView.GetNodeAt(targetPoint);

            if (targetNode != null)
            {
                treeView.SelectedNode = targetNode;
            }
        }

        private void TreeViewMenus_DragDrop(object sender, DragEventArgs e)
        {
            TreeView treeView = sender as TreeView;
            Point targetPoint = treeView.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeView.GetNodeAt(targetPoint);
            TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));

            if (draggedNode != null && targetNode != null && draggedNode != targetNode)
            {
                JObject draggedObject = (JObject)draggedNode.Tag;

                if (draggedNode.Parent == null && targetNode.Parent == null) // Reordering sections
                {
                    JArray sections = (JArray)menuJson["ui"];

                    // Remove dragged section from its current position
                    sections.Remove(draggedObject);

                    // Insert the section at the new position
                    int targetIndex = targetNode.Index;
                    sections.Insert(targetIndex, draggedObject);

                    // Rearrange TreeView nodes
                    treeView.Nodes.Remove(draggedNode);
                    treeView.Nodes.Insert(targetIndex, draggedNode);
                }
                else if (draggedNode.Parent != null && draggedNode.Parent.Tag is JObject sourceSectionObject)
                {
                    // Reordering within the same section
                    if (draggedNode.Parent == targetNode.Parent)
                    {
                        JArray items = (JArray)sourceSectionObject["items"];

                        // Remove dragged item from its current position
                        items.Remove(draggedObject);

                        // Insert at the target position
                        int targetIndex = targetNode.Index;
                        items.Insert(targetIndex, draggedObject);

                        // Rearrange TreeView nodes
                        TreeNode parentNode = draggedNode.Parent;
                        parentNode.Nodes.Remove(draggedNode);
                        parentNode.Nodes.Insert(targetIndex, draggedNode);
                    }
                    else if (targetNode.Parent != null && targetNode.Parent.Tag is JObject targetParentSectionObject)
                    {
                        // Moving between sections to a specific position
                        if (sourceSectionObject.ContainsKey("items") && targetParentSectionObject.ContainsKey("items"))
                        {
                            JArray sourceItems = (JArray)sourceSectionObject["items"];
                            JArray targetItems = (JArray)targetParentSectionObject["items"];

                            // Remove from source section
                            sourceItems.Remove(draggedObject);

                            // Insert into target section at the specific position
                            int targetIndex = targetNode.Index;
                            targetItems.Insert(targetIndex, draggedObject);

                            // Update TreeView nodes
                            TreeNode newNode = (TreeNode)draggedNode.Clone();
                            targetNode.Parent.Nodes.Insert(targetIndex, newNode);
                            draggedNode.Remove();
                        }
                    }
                    else if (targetNode.Tag is JObject targetSectionObjectDirect && targetSectionObjectDirect.ContainsKey("items"))
                    {
                        // Moving to a section directly (append to the end)
                        JArray sourceItems = (JArray)sourceSectionObject["items"];
                        JArray targetItems = (JArray)targetSectionObjectDirect["items"];

                        // Remove from source section
                        sourceItems.Remove(draggedObject);

                        // Add to target section
                        targetItems.Add(draggedObject);

                        // Add to TreeView
                        TreeNode newNode = (TreeNode)draggedNode.Clone();
                        targetNode.Nodes.Add(newNode);
                        draggedNode.Remove();
                    }
                }
            }

            // Refresh TreeView to reflect changes
            PopulateTreeView();
        }



        private void AdjustHeightToContent(object sender, EventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Calculate new height based on content
                int newHeight = TextRenderer.MeasureText(textBox.Text, textBox.Font,
                    new System.Drawing.Size(textBox.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                // Set minimum height to avoid collapsing when empty
                textBox.Height = Math.Max(newHeight + 10, 40); // Adjust padding as needed
            }
        }


        private void LoadJson()
        {
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aimultifool-Action-Menu.json");

            if (File.Exists(jsonFilePath))
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                menuJson = JObject.Parse(jsonContent);
            }
            else
            {
                CustomMessageBox.Show("JSON file not found!", "Error", this, isOkOnly: true);

                menuJson = new JObject(new JProperty("ui", new JArray()));
            }
        }

        private void PopulateTreeView()
        {
            var expandedNodes = GetExpandedNodes(treeViewMenus);

            treeViewMenus.Nodes.Clear();

            JArray sections = (JArray)menuJson["ui"];
            foreach (JObject section in sections)
            {
                TreeNode sectionNode = new TreeNode(section["sectionName"].ToString())
                {
                    Tag = section
                };

                JArray items = (JArray)section["items"];
                foreach (JObject item in items)
                {
                    TreeNode itemNode = new TreeNode(item["itemName"].ToString())
                    {
                        Tag = item
                    };
                    sectionNode.Nodes.Add(itemNode);
                }

                treeViewMenus.Nodes.Add(sectionNode);
            }

            RestoreExpandedNodes(treeViewMenus, expandedNodes);
        }

        private HashSet<string> GetExpandedNodes(TreeView treeView)
        {
            var expandedNodes = new HashSet<string>();
            foreach (TreeNode node in treeView.Nodes)
            {
                AddExpandedNodes(node, expandedNodes);
            }
            return expandedNodes;
        }

        private void AddExpandedNodes(TreeNode node, HashSet<string> expandedNodes)
        {
            if (node.IsExpanded)
            {
                expandedNodes.Add(node.FullPath);
            }
            foreach (TreeNode child in node.Nodes)
            {
                AddExpandedNodes(child, expandedNodes);
            }
        }

        private void RestoreExpandedNodes(TreeView treeView, HashSet<string> expandedNodes)
        {
            foreach (TreeNode node in treeView.Nodes)
            {
                RestoreNodeExpansion(node, expandedNodes);
            }
        }

        private void RestoreNodeExpansion(TreeNode node, HashSet<string> expandedNodes)
        {
            if (expandedNodes.Contains(node.FullPath))
            {
                node.Expand();
            }
            foreach (TreeNode child in node.Nodes)
            {
                RestoreNodeExpansion(child, expandedNodes);
            }
        }

        private void TreeViewMenus_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is JObject selectedObject)
            {
                isProgrammaticUpdate = true; // Disable validation temporarily

                if (selectedObject.ContainsKey("items"))
                {
                    // Section selected
                    txtSectionName.Enabled = true;
                    txtSectionName.Text = selectedObject["sectionName"]?.ToString();

                    txtItemName.Enabled = false;
                    txtPrompt.Enabled = false;
                    txtItemName.Text = string.Empty;
                    txtPrompt.Text = string.Empty;
                }
                else
                {
                    // Item selected
                    string parentSectionName = e.Node.Parent?.Tag is JObject parentSection
                        ? parentSection["sectionName"]?.ToString()
                        : null;

                    txtSectionName.Enabled = false;

                    txtItemName.Enabled = true;
                    txtItemName.Text = selectedObject["itemName"]?.ToString();

                    if (parentSectionName == "Modifiers")
                    {
                        txtPrompt.Enabled = false; // Disable the Prompt text field for Modifiers items
                        txtPrompt.Text = string.Empty; // Optionally clear the text
                    }
                    else
                    {
                        txtPrompt.Enabled = true; // Enable the Prompt text field for other sections
                        txtPrompt.Text = selectedObject["prompt"]?.ToString();
                    }
                }

                isProgrammaticUpdate = false; // Re-enable validation
            }
        }



        // Updated method to add a new section
        private void BtnAddSection_Click(object sender, EventArgs e)
        {
            string defaultName = "New Section";

            // Check if the default name conflicts with restricted names
            if (restrictedSectionNames.Contains(defaultName))
            {
                CustomMessageBox.Show($"Cannot use the name '{defaultName}' as it conflicts with a system menu.", "Error", this, isOkOnly: true);

                return;
            }

            JObject newSection = new JObject
            {
                ["sectionName"] = defaultName,
                ["items"] = new JArray()
            };

            JArray sections = (JArray)menuJson["ui"];
            sections.Add(newSection);

            PopulateTreeView();
        }

        private void BtnAddItem_Click(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedSection &&
                selectedSection.ContainsKey("items"))
            {
                JObject newItem = new JObject
                {
                    ["itemName"] = "New Item",
                    ["prompt"] = "New Prompt"
                };

                JArray items = (JArray)selectedSection["items"];
                items.Add(newItem);

                PopulateTreeView();
            }
            else
            {
                CustomMessageBox.Show("Please select a section to add an item.", "Error", this, isOkOnly: true);

            }
        }
        private void BtnRemove_Click(object sender, EventArgs e)
        {
            // Check if the selected node is the "Modifiers" section
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedObject)
            {
                string sectionName = selectedObject["sectionName"]?.ToString();

                // Prevent deletion of the "Modifiers" section
                if (sectionName == "Modifiers")
                {
                    CustomMessageBox.Show($"The '{sectionName}' menu is a system menu and cannot be deleted.", "Error", this, isOkOnly: true);
                    return;
                }
            }

            // Proceed with deletion for other nodes
            if (treeViewMenus.SelectedNode?.Parent?.Tag is JObject parentSection &&
                parentSection.ContainsKey("items"))
            {
                JArray items = (JArray)parentSection["items"];
                items.Remove((JObject)treeViewMenus.SelectedNode.Tag);

                PopulateTreeView();
            }
            else if (treeViewMenus.SelectedNode?.Tag is JObject selectedSectionToRemove)
            {
                JArray sections = (JArray)menuJson["ui"];
                sections.Remove(selectedSectionToRemove);

                PopulateTreeView();
            }
        }



        // Real-time update of Prompt
        private void TxtPrompt_TextChanged(object sender, EventArgs e)
        {
            if (treeViewMenus.SelectedNode?.Tag is JObject selectedObject &&
                !selectedObject.ContainsKey("items")) // Ensure it's an item
            {
                selectedObject["prompt"] = txtPrompt.Text; // Update the prompt in the JSON object
            }
        }

        private void BtnSaveChanges_Click(object sender, EventArgs e)
        {
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aiMultiFool-Action-Menu.json");

            // Ensure the JSON structure is cleaned up before saving
            JArray sections = (JArray)menuJson["ui"];
            foreach (JObject section in sections)
            {
                section.Remove("itemName");
                section.Remove("prompt");
            }

            // Save the updated JSON to the file
            File.WriteAllText(jsonFilePath, menuJson.ToString(Formatting.Indented));
            //MessageBox.Show("Menu saved successfully!");

            // Trigger the menus to refresh on the main form
            OnMenusSaved?.Invoke();

            // Close the editor
            //this.Close();
        }



        // Updated method to handle real-time section name updates
        private void TxtSectionName_TextChanged(object sender, EventArgs e)
        {
            if (isProgrammaticUpdate) return; // Skip validation during programmatic updates

            if (treeViewMenus.SelectedNode?.Tag is JObject selectedObject &&
                selectedObject.ContainsKey("items")) // Ensure it's a section
            {
                string originalName = selectedObject["sectionName"]?.ToString();
                string newSectionName = txtSectionName.Text;

                // Prevent renaming the "Modifiers" section
                if (originalName == "Modifiers" && newSectionName != originalName)
                {
                    CustomMessageBox.Show($"The '{originalName}' menu is a system menu and cannot be renamed.", "Error", this, isOkOnly: true);
                    txtSectionName.Text = originalName; // Revert to the original name
                    return;
                }

                // Validate restricted names
                if (restrictedSectionNames.Contains(newSectionName))
                {
                    CustomMessageBox.Show($"The name '{newSectionName}' is reserved and cannot be used.", "Error", this, isOkOnly: true);
                    txtSectionName.Text = originalName; // Revert to the original name
                    return;
                }

                selectedObject["sectionName"] = newSectionName;
                treeViewMenus.SelectedNode.Text = newSectionName; // Update the TreeView node text
            }
        }



        // Helper method to check for existing section names
        private bool SectionNameExists(string sectionName)
        {
            JArray sections = (JArray)menuJson["ui"];
            foreach (JObject section in sections)
            {
                if (section["sectionName"]?.ToString().Equals(sectionName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
            return false;
        }
        private void MenuEditorForm_Load(object sender, EventArgs e)
        {

        }
        private void ApplyColors(string backgroundColor, string fontColor)
        {
            try
            {
                Color bgColor = ColorTranslator.FromHtml(backgroundColor);
                Color fgColor = ColorTranslator.FromHtml(fontColor);

                // Apply colors to controls
                // this.BackColor = bgColor;
                treeViewMenus.BackColor = bgColor;
                treeViewMenus.ForeColor = fgColor;

                txtSectionName.BackColor = bgColor;
                txtSectionName.ForeColor = fgColor;

                txtItemName.BackColor = bgColor;
                txtItemName.ForeColor = fgColor;

                txtPrompt.BackColor = bgColor;
                txtPrompt.ForeColor = fgColor;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error applying colors: {ex.Message}", "Error", this, isOkOnly: true);

                ApplyDefaultColors();
            }
        }

        private void ApplyDefaultColors()
        {
            Color defaultBgColor = Color.FromArgb(234, 234, 234);

            Color defaultFgColor = Color.Black;

            //this.BackColor = defaultBgColor;
            treeViewMenus.BackColor = defaultBgColor;
            treeViewMenus.ForeColor = defaultFgColor;

            txtSectionName.BackColor = defaultBgColor;
            txtSectionName.ForeColor = defaultFgColor;

            txtItemName.BackColor = defaultBgColor;
            txtItemName.ForeColor = defaultFgColor;

            txtPrompt.BackColor = defaultBgColor;
            txtPrompt.ForeColor = defaultFgColor;
        }
        private void LoadSettings()
        {
            string settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aiMultiFool-Settings.json");

            if (File.Exists(settingsFilePath))
            {
                try
                {
                    string settingsContent = File.ReadAllText(settingsFilePath);
                    settingsJson = JObject.Parse(settingsContent);

                    // Extract BackgroundColor and FontColor
                    string backgroundColor = settingsJson["BackgroundColor"]?.ToString() ?? "#EAEAEA";
                    string fontColor = settingsJson["FontColor"]?.ToString() ?? "Black";

                    // Apply the colors to the TreeView and TextBoxes
                    ApplyColors(backgroundColor, fontColor);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error loading settings: {ex.Message}", "Error", this, isOkOnly: true);

                    ApplyDefaultColors();
                }
            }
            else
            {
                ApplyDefaultColors();
            }
        }

        private void MenuEditorForm_Load_1(object sender, EventArgs e)
        {

        }
    }
}
