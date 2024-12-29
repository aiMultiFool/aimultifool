using ImageMagick;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace aimultifool
{
    public partial class EditPNG : Form
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public string UpdatedJson { get; private set; }
        private int lastSearchIndex = -1; // Track the last search index
        private JObject settingsJson;

        private RichTextBox jsonEditor; // Declare RichTextBox at class level for reference
        private TextBox searchBox; // Declare TextBox for reference

        public EditPNG(string json)
        {
            // Configure form
            this.MinimumSize = new Size(1000, 700);
            this.Icon = aimultifool.Resource.aimultifool;

            // Enable dark mode for the window
            EnableDarkMode();

            // Configure RichTextBox
            jsonEditor = new RichTextBox
            {
                Dock = DockStyle.Fill, // Fill the remaining space
                Text = json,
                Font = new Font("Consolas", 10),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Both,
            };

            // Configure Search Panel
            Panel searchPanel = new Panel
            {
                Dock = DockStyle.Top, // Dock at the top of the form
                Height = 40
            };

            searchBox = new TextBox
            {
                Width = 200,
                Left = 10,
                Top = 10,
                Margin = new Padding(10)
            };

            Button searchButton = new Button
            {
                Text = "Search",
                Left = 220,
                Top = 10,
                Width = 100,
                Height = 24,
                Margin = new Padding(10)
            };

            searchButton.Click += (s, e) =>
            {
                string searchText = searchBox.Text;
                if (!string.IsNullOrEmpty(searchText))
                {
                    // Search for the next occurrence
                    int startIndex = jsonEditor.Text.IndexOf(searchText, lastSearchIndex + 1, StringComparison.OrdinalIgnoreCase);

                    // If no more occurrences, wrap around to the beginning
                    if (startIndex == -1)
                    {
                        startIndex = jsonEditor.Text.IndexOf(searchText, 0, StringComparison.OrdinalIgnoreCase);
                    }

                    if (startIndex >= 0)
                    {
                        lastSearchIndex = startIndex; // Update last search index
                        jsonEditor.Select(startIndex, searchText.Length);
                        jsonEditor.ScrollToCaret();
                        jsonEditor.Focus();
                    }
                    else
                    {
                        MessageBox.Show("No more occurrences found.", "Search Result", MessageBoxButtons.OK);
                        lastSearchIndex = -1; // Reset the index if not found
                    }
                }
            };

            Button exportJsonButton = new Button
            {
                Text = "Export JSON",
                Left = 330,
                Top = 10,
                Width = 120,
                Height = 24,
                Margin = new Padding(10)
            };

            exportJsonButton.Click += (s, e) =>
            {
                try
                {
                    // Ensure the default folder exists
                    string defaultFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "cards-json");
                    Directory.CreateDirectory(defaultFolderPath);

                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.InitialDirectory = defaultFolderPath; // Set default folder
                        saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                        saveFileDialog.Title = "Export JSON";
                        saveFileDialog.FileName = "exported.json"; // Default file name

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            // Save the content of the JSON editor to the selected file
                            File.WriteAllText(saveFileDialog.FileName, jsonEditor.Text);
                            CustomMessageBox.Show($"JSON exported successfully to:\n{saveFileDialog.FileName}", "Export Successful", this, isOkOnly: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error exporting JSON: {ex.Message}", "Error", this, isOkOnly: true);
                }
            };

            Button saveJsonToPngButton = new Button
            {
                Text = "Inject JSON to Another PNG",
                Left = 460,
                Top = 10,
                Width = 180,
                Height = 24,
                Margin = new Padding(10)
            };

            saveJsonToPngButton.Click += (s, e) =>
            {
                try
                {
                    // Step 1: Configure OpenFileDialog to select the target PNG file
                    using (OpenFileDialog openFileDialog = new OpenFileDialog
                    {
                        Filter = "PNG Files (*.png)|*.png",
                        Title = "Select a PNG File to Inject JSON"
                    })
                    {
                        if (openFileDialog.ShowDialog() != DialogResult.OK)
                        {
                            return; // User canceled
                        }

                        string targetPngFilePath = openFileDialog.FileName;

                        // Step 2: Retrieve the JSON from the editor
                        string updatedJson = jsonEditor.Text;

                        // Step 3: Validate and encode the updated JSON
                        string base64UpdatedData;
                        try
                        {
                            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(updatedJson); // Ensure valid JSON
                            base64UpdatedData = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson)); // Encode in Base64
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"Invalid JSON format: {ex.Message}", "Error", this, isOkOnly: true);
                            return;
                        }

                        // Step 4: Embed the Base64-encoded JSON into the selected PNG file
                        try
                        {
                            using (var image = new MagickImage(targetPngFilePath))
                            {
                                image.SetAttribute("chara", base64UpdatedData); // Embed JSON metadata
                                image.Write(targetPngFilePath); // Save the updated image
                            }

                            CustomMessageBox.Show($"JSON metadata successfully saved into:\n{targetPngFilePath}", "Success", this, isOkOnly: true);
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"An error occurred while saving metadata: {ex.Message}", "Error", this, isOkOnly: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", this, isOkOnly: true);
                }
            };


            searchPanel.Controls.Add(saveJsonToPngButton);
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(searchButton);
            searchPanel.Controls.Add(exportJsonButton);

            // Add controls to the form in proper order
            this.Controls.Add(jsonEditor); // Add RichTextBox first
            this.Controls.Add(searchPanel); // Add Panel on top (ensures correct layout)

            // Set searchButton as default for Enter key
            this.AcceptButton = searchButton;

            // Configure Save Button
            Button saveBackToPngButton = new Button
            {
                Text = "Inject JSON Back to PNG",
                Left = 650, // Adjust position to fit next to other buttons
                Top = 10,
                Width = 150,
                Height = 24,
                Margin = new Padding(10)
            };
            saveBackToPngButton.Click += (s, e) =>
            {
                try
                {
                    // Get the PNG file path from the Tag property
                    string pngFilePath = this.Tag as string;

                    if (string.IsNullOrEmpty(pngFilePath))
                    {
                        CustomMessageBox.Show("No PNG file path available. Please open a PNG file first.", "Error", this, isOkOnly: true);
                        return;
                    }

                    // Retrieve the JSON from the editor
                    string updatedJson = jsonEditor.Text;

                    // Validate and encode the updated JSON
                    string base64UpdatedData;
                    try
                    {
                        var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(updatedJson); // Ensure valid JSON
                        base64UpdatedData = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson)); // Encode in Base64
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Invalid JSON format: {ex.Message}", "Error", this, isOkOnly: true);
                        return;
                    }

                    // Embed the Base64-encoded JSON into the original PNG file
                    try
                    {
                        using (var image = new MagickImage(pngFilePath))
                        {
                            image.SetAttribute("chara", base64UpdatedData); // Embed JSON metadata
                            image.Write(pngFilePath); // Save the updated image
                        }

                        CustomMessageBox.Show($"JSON metadata successfully saved back to the PNG:\n{pngFilePath}", "Success", this, isOkOnly: true);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"An error occurred while saving metadata: {ex.Message}", "Error", this, isOkOnly: true);
                    }

                    // Close the form after successful save
                    //this.DialogResult = DialogResult.OK;
                    //this.Close();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", this, isOkOnly: true);
                }
            };
            searchPanel.Controls.Add(saveBackToPngButton);



            // Load settings and apply colors
            LoadSettings();
        }

        private void EnableDarkMode()
        {
            // Set dark mode for the window using DWM API
            if (Environment.OSVersion.Version.Major >= 10) // Ensure Windows 10 or later
            {
                int useImmersiveDarkMode = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            }
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
                    string backgroundColor = settingsJson["BackgroundColor"]?.ToString() ?? "#DDDDDD";
                    string fontColor = settingsJson["FontColor"]?.ToString() ?? "Black";

                    // Apply the colors to the controls
                    ApplyColors(backgroundColor, fontColor);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButtons.OK);
                    ApplyDefaultColors();
                }
            }
            else
            {
                ApplyDefaultColors();
            }
        }

        private void ApplyDefaultColors()
        {
            Color defaultBgColor = ColorTranslator.FromHtml("#FFFFFF");
            Color defaultFgColor = Color.Black;

            jsonEditor.BackColor = defaultBgColor;
            jsonEditor.ForeColor = defaultFgColor;

            searchBox.BackColor = defaultBgColor;
            searchBox.ForeColor = defaultFgColor;
        }

        private void ApplyColors(string backgroundColor, string fontColor)
        {
            try
            {
                Color bgColor = ColorTranslator.FromHtml(backgroundColor);
                Color fgColor = ColorTranslator.FromHtml(fontColor);

                jsonEditor.BackColor = bgColor;
                jsonEditor.ForeColor = fgColor;

                searchBox.BackColor = bgColor;
                searchBox.ForeColor = fgColor;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying colors: {ex.Message}", "Error", MessageBoxButtons.OK);
                ApplyDefaultColors();
            }
        }
    }
}
