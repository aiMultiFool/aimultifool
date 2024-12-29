using ImageMagick;
using JsonRepairUtils;
using LLama;
using LLama.Common;
using LLama.Sampling;
using LLama.Transformers;
using Newtonsoft.Json; // Import Newtonsoft.Json for JSON parsing
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace aimultifool
{
    public partial class MainForm : Form
    {
        private RichTextBox txtConsole = null!;
        private RichTextBox txtUserInput = null!;
        private Button btnStop = null!;
        private Button btnContinue = null!;
        private Button btnClear = null!;
        private Button btnModify = null!;
        private Label tpsLabel = null!;
        private Button btnRestartCard = null!;
        private Button btnInjectJSON = null!;
        private Button btnExportJSON = null!;
        private Button btnPlayJSON = null!;
        private Button btnRewind = null!;
        private FlowLayoutPanel buttonPanel;  // FlowLayoutPanel for buttons
        private CancellationTokenSource cancellationTokenSource;
        private ChatSession session;
        private InferenceParams inferenceParams;
        private bool isLoading = false;
        private string selectedModelPath = "";
        private TableLayoutPanel parametersPanel;
        private TableLayoutPanel mainPanel;
        private List<ToolStripMenuItem> customMenus = new List<ToolStripMenuItem>();
        private LLamaWeights model;
        private LLamaContext context;
        private List<Tuple<string, string>> chatHistory = new List<Tuple<string, string>>();
        private List<ChatHistory.Message> contextBuffer = new List<ChatHistory.Message>();
        private bool isProcessingInput = false;
        private int tokenCount = 0;
        private DateTime startTime = DateTime.MinValue;
        private System.Windows.Forms.Timer titleUpdateTimer;
        private System.Windows.Forms.Timer jsonDetectionTimer;
        private PictureBox pictureBox;
        private string version = "0.8.51.0";
        private MenuEditorForm menuEditor;
        private string jsonContent;
        private string jsonRestartCard;
        private bool loadedfromCard = false;
        private LLamaTimings llamaTimings;
        private Stopwatch stopwatch;
        private string detectedJson = null!;
        private double peakTokensPerSecond = 0; // Track the peak tokens per second
        private PictureBox graphPictureBox;
        private int currentY; // Current Y-coordinate of the graph
        private const int stepSize = 1; // Step size for movement
        private Bitmap graphBitmap;
        private int xPosition = 0;
        private double maxVisibleTPS = 50.0;
        private Graphics graphGraphics;
        private int previousYValue = 0;
        private int totalSessionTokens = 0; // Add a field to track total tokens for the session
        private List<int> sessionTokenHistory = new List<int>(); // List to store token counts for final updates
        private uint contextSize = 0; // Context size for percentage calculation
        private bool warningFired = false; // Flag to ensure the warning is fired only once
        private ModifierForm _modifierForm; // Field to track the open ModifierForm instance
        bool isMouseDown = false;
        private string talk = "0.0";
        private string depth = "0";

        // Define constants for scroll handling
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_VSCROLL = 0x0115;
        private const int SB_PAGEBOTTOM = 7;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        //force dark mode
        public static void EnableDarkMode(IntPtr handle)
        {
            int attributeValue = 1; // 1 = Enable Dark Mode, 0 = Disable Dark Mode
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int));
        }
        public static class RandomSeedGenerator
        {
            /// Generates a robust random seed using multiple sources of entropy.
            public static int GenerateSeed()
            {
                // Combine various entropy sources
                int timeBasedSeed = (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF); // Lower 32 bits of current ticks
                int threadId = Environment.CurrentManagedThreadId; // Current thread ID
                int processId = Process.GetCurrentProcess().Id; // Process ID

                // Generate cryptographic randomness
                byte[] cryptoBytes = new byte[4]; // 4 bytes for a 32-bit integer
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(cryptoBytes);
                }
                int cryptoRandom = BitConverter.ToInt32(cryptoBytes, 0);

                // Combine all sources into one seed
                int combinedSeed = timeBasedSeed ^ threadId ^ processId ^ cryptoRandom;

                return combinedSeed;
            }
        }
        public class aimultifoolConfig
        {
            public int GpuLayerCount { get; set; }
            public int ContextSize { get; set; }
            public float Temperature { get; set; }
            public int TopK { get; set; }
            public float TopP { get; set; }
            public float MinP { get; set; }
            public int MaxTokens { get; set; }
            public float RepeatPenalty { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]

        public struct LLamaTimings
        {
            private int n_sample;

            public readonly int TokensSampled => n_sample;

            public LLamaTimings(int tokensSampled)
            {
                n_sample = tokensSampled;
            }

            public LLamaTimings IncrementTokensSampled(int count)
            {
                return new LLamaTimings(n_sample + count);
            }
        }
        public MainForm()
        {

            EnableDarkMode(this.Handle); // Apply dark mode
            InitializeComponent();
            EnsureJsonFileExists();
            LoadMenuItems();
            CreateRequiredFolders();

            // Define menu items and their corresponding Unicode symbols
            var menuItemsWithSymbols = new Dictionary<ToolStripMenuItem, string>
                {
                { saveChatSessionToolStripMenuItem1, "💾" }, // Save
                { loadChatSessionToolStripMenuItem, "📂" }, // Load
                { loadSillyTavernV2PNGCharacterCardToolStripMenuItem, "📂" }, // Load
                { LoadSillyTavernV2JSONCharacterCardToolStripMenuItem, "📂" }, // Load
                { loadNewModelToolStripMenuItem, "📂" }, // Load
                { downloadRocinante12Bv11Q4KMggufToolStripMenuItem, "📥" }, // Load
                { downloadLlama323BInstructuncensoredQ4KMggufToolStripMenuItem, "📥" }, // Load
                { createSillyTavernPNGCardToolStripMenuItem, "💉" }, // Load
                { chatGPTAICharacterCardGeneratorToolStripMenuItem, "🔗" }, // Load
                { editSillyTavernPNGCardMetadataToolStripMenuItem, "📝" }, // Load
                { menuEditorToolStripMenuItem1, "📝" }, // Load
                { windowColourToolStripMenuItem, "🎨" }, // Load
                { fontColourToolStripMenuItem, "🎨" }, // Load
                { fontTypeToolStripMenuItem, "🔤" }, // Load
                { quickstartGuideToolStripMenuItem1, "ℹ️" }, // Load
                { aIModelsGGUFToolStripMenuItem, "🔗" }, // Load
                { aICharacterCardsToolStripMenuItem1, "🔗" }, // Load
                { aiMultiFoolcomToolStripMenuItem1, "🔗" }, // Load
                };

            // Set images for these menu items
            SetUnicodeImageForMenuItems(menuItemsWithSymbols);

            // Initialize the JSON Detection Timer
            jsonDetectionTimer = new System.Windows.Forms.Timer();
            jsonDetectionTimer.Interval = 500; // Check every 500ms
            jsonDetectionTimer.Tick += JsonDetectionTimer_Tick;
            jsonDetectionTimer.Start();

            // Set the minimum size of the form
            this.MinimumSize = new Size(1092, 785);
            this.ShowIcon = true;

            // Initialize and set up mainPanel
            mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.None; // Allow manual positioning
            mainPanel.Location = new Point(2, 22); // Adjust X and Y coordinates to fit your layout
            mainPanel.Size = new Size(this.ClientSize.Width - 108, this.ClientSize.Height - 160); // Adjust width and height to fit remaining space

            // Optional: Ensure mainPanel resizes with the form
            mainPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Set up columns and styles
            mainPanel.ColumnCount = 1;

            // Add mainPanel to the form
            this.Controls.Add(mainPanel);

            InitializeCustomComponents();

            this.Text = "aiMultiFool " + version;
            txtConsole.Font = new Font("Segoe UI", 10.0f);
            txtUserInput.Font = new Font("Segoe UI", 10.0f);

            LoadSettings();

            InitializeGraphBitmap();

            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf", "Card Roleplay Uncensored Rocinante-12B-v1.1-Q4_K_M.gguf");

            // Check if the file exists and remove the corresponding menu item
            if (File.Exists(filePath))
            {
                fileToolStripMenuItem.DropDownItems.Remove(downloadRocinante12Bv11Q4KMggufToolStripMenuItem);
            }

            string filePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf", "General Chat Censored Llama-3.2-3B-Instruct-Q4_K_M.gguf");

            // Check if the file exists and remove the corresponding menu item
            if (File.Exists(filePath2))
            {
                fileToolStripMenuItem.DropDownItems.Remove(downloadLlama323BInstructuncensoredQ4KMggufToolStripMenuItem);
            }

            // Check if both items are removed and remove the separator if needed
            if (!fileToolStripMenuItem.DropDownItems.Contains(downloadRocinante12Bv11Q4KMggufToolStripMenuItem) &&
                !fileToolStripMenuItem.DropDownItems.Contains(downloadLlama323BInstructuncensoredQ4KMggufToolStripMenuItem))
            {
                fileToolStripMenuItem.DropDownItems.Remove(toolStripMenuItem2);
            }

        }

        private void SetUnicodeImageForMenuItems(Dictionary<ToolStripMenuItem, string> menuItemsWithSymbols)
        {
            foreach (var kvp in menuItemsWithSymbols)
            {
                ToolStripMenuItem menuItem = kvp.Key;
                string unicodeSymbol = kvp.Value;

                // Create a high-resolution image from the Unicode symbol
                Bitmap highResImage = CreateSymbolImage(unicodeSymbol);

                // Assign the image to the menu item
                menuItem.Image = highResImage;

                // Prevent the menu item from resizing or scaling the image
                menuItem.ImageScaling = ToolStripItemImageScaling.None;
            }
        }
        private Bitmap CreateSymbolImage(string symbol)
        {
            int size = 15; // Use a higher resolution for better rendering clarity
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent); // Transparent background
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; // Anti-aliased text rendering
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (Font font = new Font("Segoe UI Emoji", 12, FontStyle.Regular, GraphicsUnit.Pixel)) // Emoji-supporting font
                {
                    // Measure the size of the rendered text to center it
                    SizeF textSize = g.MeasureString(symbol, font);
                    float x = (size - textSize.Width) / 2; // Center horizontally
                    float y = (size - textSize.Height) / 2; // Center vertically

                    // Draw the Unicode symbol
                    g.DrawString(symbol, font, Brushes.Black, x, y);
                }
            }
            return bmp;
        }

        private void CreateRequiredFolders()
        {
            // Define the folder names
            string[] folders = { "cards-json", "models-gguf", "chats-json", "cards-png" };

            // Get the app working directory
            string appWorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Loop through and ensure each folder exists
            foreach (string folder in folders)
            {
                string folderPath = Path.Combine(appWorkingDirectory, folder);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    Console.WriteLine($"Created folder: {folderPath}");
                }
            }
        }

        // JSON Detection Timer Tick Event
        private void JsonDetectionTimer_Tick(object sender, EventArgs e)
        {
            if (btnStop.Enabled)
            {
                if (btnExportJSON.Enabled)
                    btnExportJSON.Enabled = false;
                if (btnPlayJSON.Enabled)
                    btnPlayJSON.Enabled = false;
                if (btnInjectJSON.Enabled)
                    btnInjectJSON.Enabled = false;

                return;
            }

            string consoleText;

            // Safely access the txtConsole's text on the UI thread
            if (txtConsole.InvokeRequired)
            {
                consoleText = txtConsole.Invoke(new Func<string>(() => txtConsole.Text));
            }
            else
            {
                consoleText = txtConsole.Text;
            }

            // Extract potential JSON fragments using regex
            string jsonPattern = @"\{(?:[^{}]|(?<o>\{)|(?<-o>\}))*?(?(o)(?!))\}";
            MatchCollection matches = Regex.Matches(consoleText, jsonPattern);

            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string possibleJson = match.Value;

                    // Try to validate or repair JSON using JsonRepairUtils
                    if (TryRepairJson(possibleJson, out string repairedJson))
                    {
                        EnableButtons(); // Enable UI buttons if JSON is valid
                        return;
                    }
                }
            }

            DisableButtons(); // Disable UI buttons if no valid JSON
        }

        private bool TryRepairJson(string json, out string repairedJson)
        {
            repairedJson = null;

            try
            {
                // Attempt direct parsing
                JToken.Parse(json);
                repairedJson = json;
                return true;
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine($"Initial JSON parsing failed: {ex.Message}");

                try
                {
                    // Detect and repair improperly escaped quotes in "height" or similar fields
                    json = RepairInvalidQuotes(json);

                    // Validate the repaired JSON
                    JToken.Parse(json);
                    repairedJson = json;
                    return true;
                }
                catch (JsonReaderException repairEx)
                {
                    Console.WriteLine($"Repair for invalid quotes failed: {repairEx.Message}");

                    try
                    {
                        // Attempt repair using JsonRepairUtils
                        var jsonRepair = new JsonRepair();

                        // First repair attempt
                        repairedJson = jsonRepair.Repair(json);

                        // Validate repaired JSON
                        JToken.Parse(repairedJson);
                        return true;
                    }
                    catch (Exception repairUtilsEx)
                    {
                        Console.WriteLine($"JsonRepairUtils failed: {repairUtilsEx.Message}");

                        try
                        {
                            // Custom fallback repair
                            repairedJson = FallbackRepair(json);

                            // Validate repaired JSON
                            JToken.Parse(repairedJson);
                            return true;
                        }
                        catch (Exception fallbackEx)
                        {
                            Console.WriteLine($"Fallback repair failed: {fallbackEx.Message}");
                            return false;
                        }
                    }
                }
            }
        }

        private string RepairInvalidQuotes(string json)
        {
            // Use regex to find fields with improperly escaped quotes, such as height descriptions
            var regex = new Regex(@"(\d+)'(\d+)""");
            var repairedJson = regex.Replace(json, match => $"{match.Groups[1].Value}'{match.Groups[2].Value}"); // Retain the height as 5'9 without double quotes

            return repairedJson;
        }

        private string FallbackRepair(string json)
        {
            // Escape single quotes and problematic characters
            json = json.Replace("'", "\\'");
            json = json.Replace("\r\n", " "); // Replace newlines
            json = json.Replace("\n", " "); // Replace newlines

            // Fix missing commas in arrays
            json = Regex.Replace(json, @"(?<=})\s*(?={)", ",");

            // Fix mismatched braces or brackets
            int openBraces = json.Count(c => c == '{');
            int closeBraces = json.Count(c => c == '}');
            int openBrackets = json.Count(c => c == '[');
            int closeBrackets = json.Count(c => c == ']');

            json += new string('}', openBraces - closeBraces);
            json += new string(']', openBrackets - closeBrackets);

            return json;
        }
        private void EnableButtons()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    btnExportJSON.Enabled = true;
                    btnPlayJSON.Enabled = true;
                    btnInjectJSON.Enabled = true;
                }));
            }
            else
            {
                btnExportJSON.Enabled = true;
                btnPlayJSON.Enabled = true;
                btnInjectJSON.Enabled = true;
            }
        }

        private void DisableButtons()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    btnExportJSON.Enabled = false;
                    btnPlayJSON.Enabled = false;
                    btnInjectJSON.Enabled = false;
                }));
            }
            else
            {
                btnExportJSON.Enabled = false;
                btnPlayJSON.Enabled = false;
                btnInjectJSON.Enabled = false;
            }
        }

        private bool TryDetectAndStoreJson()
        {
            // Ensure we're operating on the latest text
            string consoleText;
            if (txtConsole.InvokeRequired)
            {
                consoleText = txtConsole.Invoke(new Func<string>(() => txtConsole.Text));
            }
            else
            {
                consoleText = txtConsole.Text;
            }

            // Regex to identify JSON objects
            string jsonPattern = @"\{(?:[^{}]|(?<o>\{)|(?<-o>\}))*?(?(o)(?!))\}";
            MatchCollection matches = Regex.Matches(consoleText, jsonPattern);

            // If no matches found, clear detectedJson and return false
            if (matches.Count == 0)
            {
                detectedJson = null;
                return false;
            }

            // Fetch the last JSON fragment
            string lastJson = matches[matches.Count - 1].Value;

            // Validate and repair the last JSON fragment
            if (TryRepairJson(lastJson, out string repairedJson))
            {
                detectedJson = repairedJson; // Store the last valid JSON
                return true;
            }

            // If the last JSON fragment is invalid, clear detectedJson
            detectedJson = null;
            return false;
        }
        private void BtnExportJSON_Click(object sender, EventArgs e)
        {
            if (detectedJson == null)
            {
                CustomMessageBox.Show("No valid JSON object found.", "Not Found", this, isOkOnly: true);
                return;
            }

            SaveJsonToFile(detectedJson);
        }

        private async void BtnPlayJSON_Click(object sender, EventArgs e)
        {
            try
            {
                // Ensure JSON detection is current
                if (detectedJson == null)
                {
                    CustomMessageBox.Show("No valid JSON object found.", "Not Found", this, isOkOnly: true);
                    return;
                }

                string v2JSON = AddDataBlock(detectedJson);

                // Show "Do you want to save?" dialog
                var result = CustomMessageBox.Show("Do you want to save the JSON Character Card before playing?", "Save JSON", this, isOkOnly: false);
                if (result == DialogResult.Yes)
                {
                    // Ensure the 'chat-history' folder exists in the working directory
                    string chatHistoryFolder = Path.Combine(Environment.CurrentDirectory, "cards-json");
                    if (!Directory.Exists(chatHistoryFolder))
                    {
                        Directory.CreateDirectory(chatHistoryFolder);
                    }

                    // Open a save file dialog
                    using (var saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                        saveFileDialog.Title = "Save Character Card";
                        saveFileDialog.DefaultExt = "json";
                        saveFileDialog.FileName = "my character card.json";

                        // Set the initial directory to the 'chat-history' folder
                        saveFileDialog.InitialDirectory = chatHistoryFolder;

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                File.WriteAllText(saveFileDialog.FileName, v2JSON);
                                CustomMessageBox.Show("Character Card saved successfully.", "Success", this, isOkOnly: true);
                            }
                            catch (Exception ex)
                            {
                                CustomMessageBox.Show($"Failed to save Chat Session: {ex.Message}", "Error", this, isOkOnly: true);
                            }
                        }
                    }
                }

                // Process the JSON for play functionality
                await ProcessJson(v2JSON);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error processing JSON: {ex.Message}", "Error", this, isOkOnly: true);
            }
        }
        private void SaveJsonToFile(string json)
        {
            string workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string saveFolder = Path.Combine(workingDirectory, "cards-json");
            Directory.CreateDirectory(saveFolder);

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Save JSON Character Card";
                saveFileDialog.InitialDirectory = saveFolder;
                saveFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "json";
                saveFileDialog.AddExtension = true;
                saveFileDialog.FileName = $"CardJson_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string v2JSON = AddDataBlock(json);
                    string filePath = saveFileDialog.FileName;
                    File.WriteAllText(filePath, v2JSON);
                    CustomMessageBox.Show("JSON Character Card saved successfully.", "Success", this, isOkOnly: true);
                }
            }
        }

        private string AddDataBlock(string jsonInput)
        {
            try
            {
                var jsonObject = JObject.Parse(jsonInput);

                // Add current date to the "create_date" field
                jsonObject["create_date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

                // Create the data block by duplicating relevant fields
                var dataBlock = new JObject
                {
                    ["name"] = jsonObject["name"],
                    ["description"] = jsonObject["description"],
                    ["personality"] = jsonObject["personality"],
                    ["scenario"] = jsonObject["scenario"],
                    ["first_mes"] = jsonObject["first_mes"],
                    ["mes_example"] = jsonObject["mes_example"],
                    ["creator_notes"] = jsonObject["creatorcomment"],
                    ["system_prompt"] = "",
                    ["post_history_instructions"] = "",
                    ["tags"] = jsonObject["tags"],
                    ["creator"] = "AI Character Card Generator",
                    ["character_version"] = "1.0",
                    ["alternate_greetings"] = new JArray(),
                    ["extensions"] = new JObject
                    {
                        ["talkativeness"] = jsonObject["talkativeness"],
                        ["fav"] = jsonObject["fav"],
                        ["world"] = "",
                        ["depth_prompt"] = new JObject
                        {
                            ["prompt"] = "",
                            ["depth"] = 4
                        }
                    }
                };

                // Insert the "data" block immediately after "spec_version"
                var newJsonObject = new JObject();
                foreach (var property in jsonObject.Properties())
                {
                    newJsonObject.Add(property.Name, property.Value);

                    if (property.Name == "spec_version")
                    {
                        // Add the "data" block after "spec_version"
                        newJsonObject.Add("data", dataBlock);
                    }
                }

                return newJsonObject.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error modifying JSON: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return jsonInput;
            }
        }

        private async Task ProcessJson(string json)
        {

            UpdatePictureBoxImage(pictureBox, null); // Use null or empty string as a signal to reset

            // Parsing and updating UI
            txtConsole.Clear();
            AppendText("AI is processing Character Card please wait..\r\n");
            txtUserInput.Clear();
            clearContext();

            StartNewChat();

            AppendText("\r\n");

            TextBox txtUser = parametersPanel.Controls["txtCard User"] as TextBox;
            TextBox txtTalkativeness = parametersPanel.Controls["txtAI Talkativeness"] as TextBox;
            TextBox txtDepthPrompt = parametersPanel.Controls["txtAI Depth"] as TextBox;

            string userName = txtUser?.Text.Trim() ?? string.Empty;

            // Parse the JSON content
            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);

            string talkativenessStr = jsonObject?.talkativeness?.ToString() ?? "0.5";
            string depthPromptStr = jsonObject?.data?.extensions?.depth_prompt?.depth?.ToString() ?? "5";

            if (txtTalkativeness != null)
                txtTalkativeness.Text = talkativenessStr;
            talk = talkativenessStr;

            if (txtDepthPrompt != null)
                txtDepthPrompt.Text = depthPromptStr;
            depth = depthPromptStr;

            string cleanedJsonContent = json.Replace("<START>\n", string.Empty)
                                            .Replace("<START>\\n", string.Empty)
                                            .Replace("{{user}}", userName);

            string finalPrompt = $@"talkativeness is the length of your replies, it is scaled between 0.1 and 1.0, with 1.0 being the most verbose. Set your talkativeness to {talkativenessStr}. depth is used to adjust response detail. It provides guidance on tone or complexity, where 1 is concise and 5 is highly detailed and immersive. Set your depth to {depthPromptStr}. Start a roleplay scenario based on the following data. Never type {{char}} or {{user}}. {cleanedJsonContent}";

            jsonContent = finalPrompt;
            jsonRestartCard = finalPrompt;
            loadedfromCard = true;

            ReloadInferenceParams();

            var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");
            // If the form is found and we're not instructed to keep it open, close it
            if (openForm != null)
            {
                openForm.Close();
            }

            await HandleInput(finalPrompt, true);
        }

        private void EnsureJsonFileExists()
        {
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aiMultiFool-Action-Menu.json");

            if (!File.Exists(jsonFilePath))
            {
                try
                {
                    // Access the JSON from resources as byte[]
                    byte[] jsonBytes = aimultifool.Resource.aimultifool_action_menu;

                    // Convert byte[] to string
                    jsonContent = System.Text.Encoding.UTF8.GetString(jsonBytes);

                    // Write it to the working directory
                    File.WriteAllText(jsonFilePath, jsonContent);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error recreating JSON file: {ex.Message}", "Error", this, isOkOnly: true);

                }
            }
        }

        private void ReloadMenus()
        {
            // Clear all dynamically added custom menus
            foreach (var menu in customMenus)
            {
                menuStrip1.Items.Remove(menu);
            }

            // Clear the tracked custom menus
            customMenus.Clear();

            // Reload menus from the JSON file
            LoadMenuItems();

            _modifierForm?.ReloadSliders();

        }


        private void LoadMenuItems()
        {
            // Path to your JSON file
            string jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "aiMultiFool-Action-Menu.json");

            if (!File.Exists(jsonFilePath))
            {
                CustomMessageBox.Show("JSON file not found!", "Error", this, isOkOnly: true);
                return;
            }

            try
            {
                // Read and parse the JSON
                jsonContent = File.ReadAllText(jsonFilePath);
                JObject uiConfig = JObject.Parse(jsonContent);

                // Locate the "ui" array
                JArray menuSections = (JArray)uiConfig["ui"];

                int insertIndex = 0; // Start adding menus at the front

                foreach (JObject section in menuSections)
                {
                    // Get the section name
                    string sectionName = section["sectionName"]?.ToString();
                    JArray items = (JArray)section["items"];

                    // Determine the Unicode symbol based on the section name
                    //string unicodeSymbol = sectionName.Contains("Tool", StringComparison.OrdinalIgnoreCase) ? "⚙️" : "🤖";

                    // Check if a menu with this name already exists
                    ToolStripMenuItem existingMenu = null;
                    foreach (ToolStripItem menuItem in menuStrip1.Items)
                    {
                        if (menuItem is ToolStripMenuItem && menuItem.Text == sectionName)
                        {
                            existingMenu = (ToolStripMenuItem)menuItem;
                            break;
                        }
                    }

                    // Create a new menu item for the section if it doesn't already exist
                    ToolStripMenuItem sectionMenuItem = existingMenu ?? new ToolStripMenuItem(sectionName);

                    // Set visibility to false if the section is "Modifiers"
                    if (sectionName.Equals("Modifiers", StringComparison.OrdinalIgnoreCase))
                    {
                        sectionMenuItem.Visible = false;
                    }

                    // Add items to this section (avoid duplicates)
                    foreach (JObject item in items)
                    {
                        string itemName = item["itemName"]?.ToString();
                        string prompt = item["prompt"]?.ToString();

                        // Check if the item is a separator
                        if (itemName == "-")
                        {
                            // Add a separator
                            sectionMenuItem.DropDownItems.Add(new ToolStripSeparator());
                            continue; // Move to the next item
                        }

                        // Check if the item already exists in the section
                        bool itemExists = false;
                        foreach (ToolStripItem menuItem in sectionMenuItem.DropDownItems)
                        {
                            if (menuItem.Text == itemName)
                            {
                                itemExists = true;
                                break;
                            }
                        }

                        if (!itemExists)
                        {
                            // Create a new ToolStripMenuItem for the item
                            ToolStripMenuItem menuItem = new ToolStripMenuItem(itemName);

                            // Set the Unicode-based image
                            //menuItem.Image = CreateSymbolImage(unicodeSymbol);
                            //menuItem.ImageScaling = ToolStripItemImageScaling.None; // Prevent scaling

                            // Attach the click event to the menu item
                            menuItem.Click += async (sender, e) =>
                            {
                                AppendText("\r\n");
                                AppendText("\r\n");
                                await HandleInput(prompt, true);
                            };

                            // Add the item to the section menu
                            sectionMenuItem.DropDownItems.Add(menuItem);
                        }
                    }

                    // Add the section menu to the front of the menu strip if it's newly created
                    if (existingMenu == null)
                    {
                        menuStrip1.Items.Insert(insertIndex, sectionMenuItem);
                        customMenus.Add(sectionMenuItem); // Track the custom menu
                        insertIndex++; // Update the insertion index for the next menu
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error loading menu: {ex.Message}", "Error", this, isOkOnly: true);
            }
        }

        public ToolStripMenuItem GetModifiersMenu()
        {
            return menuStrip1.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text.Equals("Modifiers", StringComparison.OrdinalIgnoreCase));
        }

        // Method to remove any potential formatting
        private string StripFormatting(string input)
        {
            // Remove RTF tags (if any)
            input = Regex.Replace(input, @"\{\\[^}]*\}", string.Empty);

            // Remove HTML tags (if any)
            input = Regex.Replace(input, "<[^>]*>", string.Empty);

            // Optionally remove any special characters like control characters
            input = new string(input.Where(c => !Char.IsControl(c)).ToArray());

            // Return the plain text
            return input.Trim();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveWindowSettings(); // Save window settings when closing
            base.OnFormClosing(e);
        }

        private void InitializeGraphBitmap()
        {
            if (graphBitmap == null) // Only initialize if it hasn't been created yet
            {
                graphBitmap = new Bitmap(graphPictureBox.Width, graphPictureBox.Height);
                using (Graphics g = Graphics.FromImage(graphBitmap))
                {
                    // Use the BackColor property of tpsLabel for the background color
                    g.Clear(tpsLabel.BackColor);
                }
                graphPictureBox.Image = graphBitmap;
            }
        }

        private void TitleUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateTokenStatsUI();
        }
        public void AppendToConsole(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendToConsole(text)));
                return;
            }

            // Append a linefeed if necessary
            txtConsole.AppendText(Environment.NewLine);
            txtConsole.AppendText(Environment.NewLine);

            // Select the end of the text to apply bold formatting
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.SelectionLength = 0;

            // Set the font style to bold
            txtConsole.SelectionFont = new Font(txtConsole.Font, FontStyle.Bold);

            // Append the text


            txtConsole.AppendText(text);
            txtConsole.AppendText(Environment.NewLine);
            // Reset the selection to default font
            txtConsole.SelectionFont = txtConsole.Font;

            // Append another linefeed
            txtConsole.AppendText(Environment.NewLine);
        }

        private void InitializeCustomComponents()
        {
            // Assume mainPanel is the panel that contains the controls
            int panelWidth = mainPanel.ClientSize.Width;
            int txtUserInputWidth = (int)(panelWidth * 0.73); // 80% of the panel's width
            int graphPictureBoxWidth = panelWidth - txtUserInputWidth - 15; // Remaining 20%, with spacing



            // Initialize the RichTextBox (txtConsole) to fill the remaining space
            txtConsole = new RichTextBox
            {
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                TabStop = false,
                BackColor = Color.White,
            };

            txtConsole.SelectionIndent = 5; // Adds padding to the left
            txtConsole.SelectionRightIndent = 5; // Adds padding to the right
            txtConsole.LinkClicked += TxtConsole_LinkClicked;

            // Initialize txtUserInput
            txtUserInput = new RichTextBox
            {
                Multiline = true,
                Enabled = false,
                Height = 92,
                Location = new Point(5, this.ClientSize.Height - 137), // Align near the bottom-left
                Width = txtUserInputWidth, // Dynamically calculated width
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right, // Resizes with the form
            };


            txtUserInput.SelectionIndent = 5; // Adds padding to the left
            txtUserInput.SelectionRightIndent = 5; // Adds padding to the right
            txtUserInput.KeyDown += TxtUserInput_KeyDown;

            graphPictureBox = new PictureBox
            {
                Width = graphPictureBoxWidth + 4, // Dynamically calculated width
                Height = txtUserInput.Height - 28, // Match the height of txtUserInput
                Location = new Point(mainPanel.ClientSize.Width - graphPictureBoxWidth - 5, txtUserInput.Top), // Align to the right
                BorderStyle = BorderStyle.Fixed3D,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom, // Anchor to the right and bottom of the panel
                BackColor = Color.White,
            };


            // Initialize and configure the context menu
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem copyMenuItem = new ToolStripMenuItem("Copy");

            // Add the "Copy" menu item to the context menu
            contextMenu.Items.Add(copyMenuItem);

            // Assign the context menu to txtConsole
            txtConsole.ContextMenuStrip = contextMenu;

            // Handle the Copy menu item's click event
            copyMenuItem.Click += (s, e) => CopySelectedText();



            // Initialize tpsLabel
            // Initialize tpsLabel
            tpsLabel = new Label
            {
                Width = graphPictureBox.Width - 1, // Align width with graphPictureBox
                //Height = 20,                      // Height of the label
                //AutoSize = true,
                BackColor = Color.White,          // Background color
                BorderStyle = BorderStyle.Fixed3D, // Add a border for visual clarity
                Location = new Point(graphPictureBox.Left, graphPictureBox.Bottom + 5), // Position below graphPictureBox
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right, // Align with graphPictureBox
                Font = new Font("Segoe UI", 7),   // Font styling
                TextAlign = ContentAlignment.MiddleCenter, // Center text horizontally and vertically
            };


            // Create buttonPanel
            buttonPanel = new FlowLayoutPanel
            {
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left, // Ensure it stays anchored
            };

            // Set Location after Height has been set
            buttonPanel.Location = new Point(7, this.ClientSize.Height - buttonPanel.Height - 5);

            // Add the buttonPanel to the form
            Controls.Add(buttonPanel);



            ToolTip buttonToolTip = new ToolTip
            {
                AutoPopDelay = 5000, // Tooltip stays visible for 5 seconds
                InitialDelay = 500,  // Delay before tooltip appears
                ReshowDelay = 100,   // Delay before it reappears after disappearing
                ShowAlways = true    // Always show tooltip, even if form is not focused
            };

            int buttonWidth = 89; // Define a standard width for all buttons

            // Initialize buttons with their respective tooltips
            btnStop = CreateButton("\u23F9", "Stop AI", buttonWidth, Color.Red, BtnStop_Click, buttonToolTip);
            btnContinue = CreateButton("\u25B6", "Continue AI", buttonWidth, Color.Green, BtnContinue_Click, buttonToolTip);
            btnRestartCard = CreateButton("↻", "Restart Character Card", buttonWidth, Color.Green, BtnRestartCard_Click, buttonToolTip);
            btnInjectJSON = CreateButton("\uD83D\uDC89", "Inject JSON data into a PNG image to create a Character Card.", buttonWidth, Color.Red, BtnInjectJSON_Click, buttonToolTip);
            btnRewind = CreateButton("\u23EA", "Rewind AI", buttonWidth, Color.Green, BtnRewind_Click, buttonToolTip);
            btnClear = CreateButton("\U0001F4AC", "Start a new chat session", buttonWidth, Color.Green, BtnClear_Click, buttonToolTip);
            btnExportJSON = CreateButton("\uD83D\uDCBE", "Save Character Card data to a JSON file", buttonWidth, Color.Red, BtnExportJSON_Click, buttonToolTip);
            btnPlayJSON = CreateButton("\U0001F916", "Play the Character Card JSON", buttonWidth, Color.Green, BtnPlayJSON_Click, buttonToolTip);
            btnModify = CreateButton("\u2728", "Custom Modifiers - These are customized from Options / Action Menu Editor", buttonWidth, Color.Green, BtnModify_Click, buttonToolTip);

            // Add buttons to the button panel
            buttonPanel.Controls.AddRange(new[] { btnStop, btnContinue, btnRewind, btnModify, btnRestartCard, btnClear, btnPlayJSON, btnExportJSON, btnInjectJSON });

            // Initialize the TableLayoutPanel for parameters
            parametersPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                ColumnCount = 1,
                Padding = new Padding(4, 4, 4, 4),
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };

            InitializeParameterTextBoxes();

            disableCardControls();

            // Add txtConsole to the mainPanel
            mainPanel.Controls.Add(txtConsole, 0, 0); // Column 0, Row 0 for txtConsole

            // Add components to the form's controls
            Controls.Add(mainPanel);
            Controls.Add(txtUserInput);
            Controls.Add(graphPictureBox); // Ensure graphPictureBox is to the right of txtUserInput
            Controls.Add(buttonPanel);
            Controls.Add(parametersPanel);
            Controls.Add(tpsLabel);

            cancellationTokenSource = new CancellationTokenSource();

            Load += async (sender, e) =>
            {
                // Ensure form is fully displayed
                this.Shown += async (s, ev) =>
                {
                    await Task.Delay(500); // Add a small delay to allow for full rendering
                    await PromptUserToSelectModel();
                };
            };
        }

        // Helper method to create buttons
        private Button CreateButton(string text, string toolTipText, int width, Color foreColor, EventHandler clickHandler, ToolTip toolTip)
        {
            Button button = new Button
            {
                Text = text,
                Height = 30,
                Width = width,
                TabStop = false,
                Enabled = false,
                ForeColor = foreColor,
                Font = new Font("Segoe UI Symbol", 12),
            };
            button.Click += clickHandler;
            toolTip.SetToolTip(button, toolTipText);
            return button;
        }

        private void CopySelectedText()
        {
            if (!string.IsNullOrEmpty(txtConsole.SelectedText))
            {
                Clipboard.SetText(txtConsole.SelectedText);
            }
            else
            {
                CustomMessageBox.Show("No text selected to copy.", "Error", this, isOkOnly: true);
            }
        }

        private void InitializeParameterTextBoxes()
        {
            // Parameter names
            string[] parameterNames = new string[]
            {
        "Temperature", "TopP", "MinP", "TopK",
        "AI Talkativeness", "AI Depth", // Moved above GpuLayerCount
        "GpuLayerCount", "ContextSize", "MaxTokens", "RepeatPenalty",
        "Card User"
            };

            // Default values
            string[] defaultValues = new string[]
            {
        "0.7", "0.95", "0.05", "40",
        "", "", // Defaults for "talkativeness" and "depth" moved
        "100", "4096", "-1", "1.1",
        "{{user}}"
            };

            // Tooltips
            string[] toolTips = new string[]
            {
        "Temperature: Controls randomness in text generation." + Environment.NewLine
        + "Higher values (e.g., 1.2) create varied output; lower values (e.g., 0.7) focus content.",
        "TopP: Probability threshold for nucleus sampling." + Environment.NewLine
        + "Defines cumulative probability for sampling; higher values allow more creativity.",
        "MinP: Minimum probability threshold for token selection." + Environment.NewLine
        + "Higher values restrict sampling to more likely tokens, reducing randomness.",
        "TopK: Limits sampling to the top K highest probability tokens." + Environment.NewLine
        + "Lower values produce more deterministic results; higher values increase diversity.",
        "Talkativeness: Adjusts the verbosity of character responses." + Environment.NewLine
        + "Higher values result in more elaborate and detailed responses, while lower values produce concise and focused replies.",
        "Depth: Controls the complexity and richness of character responses." + Environment.NewLine
        + "Higher depth values (e.g., 5) lead to immersive and intricate replies, while lower values (e.g., 1) keep responses simple and straightforward.",
        "GpuLayerCount: Sets the number of GPU layers utilized for processing." + Environment.NewLine
        + "Higher values improve performance on GPU systems but require more memory.",
        "ContextSize: Defines the maximum number of tokens for context." + Environment.NewLine
        + "Higher values improve coherence for longer text but increase memory use.",
        "MaxTokens: Maximum tokens generated per response. Use -1 for no limit." + Environment.NewLine
        + "A smaller value controls verbosity in output length.",
        "RepeatPenalty: Applies a penalty to discourage repeated tokens." + Environment.NewLine
        + "Values above 1.0 reduce repetition; 1.0 means no penalty.",
        "For Character Card loading. Replaces {{user}} with your name." + Environment.NewLine
        + "Usually required.",
            };

            // Create and configure PictureBox
            pictureBox = new PictureBox
            {
                Size = new System.Drawing.Size(95, 95),
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderStyle = BorderStyle.Fixed3D,
                Cursor = Cursors.Hand, // Change cursor to link hand
                BackColor = Color.Black // Sets the background to black
            };

            try
            {
                pictureBox.Image = aimultifool.Resource.aimultifool5;
                pictureBox.Tag = "aimultifool5";
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to load image: {ex.Message}", "Error", this, isOkOnly: true);
            }
            // Add click event handler to distinguish between resource and local file images
            pictureBox.Click += (sender, e) =>
            {
                try
                {
                    // Check if the PictureBox has an image
                    if (pictureBox.Image != null)
                    {
                        // Determine if the image is a local file or a resource
                        if (pictureBox.Tag is string filePath && File.Exists(filePath))
                        {
                            // Local file image: open it in CardImage form
                            Image image = Image.FromFile(filePath);

                            // Extract the filename without extension
                            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                            // Display the image in the CardImage form
                            CardImage cardImageForm = new CardImage
                            {
                                PictureBox = { Image = image }
                            };
                            // Set the form's title
                            cardImageForm.Text = $"Now Playing - {fileNameWithoutExtension}";

                            // Calculate and set form size based on image aspect ratio
                            int initialFormWidth = 600;
                            float aspectRatio = (float)image.Height / image.Width;
                            int calculatedHeight = (int)(initialFormWidth * aspectRatio);
                            int borderWidth = cardImageForm.Width - cardImageForm.ClientSize.Width;
                            int titleBarHeight = cardImageForm.Height - cardImageForm.ClientSize.Height;

                            cardImageForm.Width = initialFormWidth + borderWidth;
                            cardImageForm.Height = calculatedHeight + titleBarHeight;

                            // Center the CardImage form on the main application's window
                            Rectangle mainFormBounds = this.Bounds;
                            int centerX = mainFormBounds.Left + (mainFormBounds.Width - cardImageForm.Width) / 2;
                            int centerY = mainFormBounds.Top + (mainFormBounds.Height - cardImageForm.Height) / 2;
                            cardImageForm.StartPosition = FormStartPosition.Manual;
                            cardImageForm.Location = new Point(centerX, centerY);

                            // Check if an instance of CardImage is already open
                            if (Application.OpenForms.OfType<CardImage>().Any())
                            {
                                // An instance is already open; no action needed
                            }
                            else
                            {
                                // No instance is open; create and show a new instance
                                cardImageForm.Show();
                            }

                        }
                        else
                        {
                            // Resource image: open the URL
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://aimultifool.com",
                                UseShellExecute = true // This ensures the default browser is used
                            });
                        }
                    }
                    else
                    {
                        // No image in PictureBox
                        CustomMessageBox.Show("No image is set in the PictureBox.", "Error", this, isOkOnly: true);
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Failed to process the click action: {ex.Message}", "Error", this, isOkOnly: true);
                }
            };

            // Add configuration for "AI Talkativeness" and "AI Depth"


            var parameterConfig = new Dictionary<string, (double Min, double Max, double? Default, bool IsInteger)>
    {
        { "Temperature", (0.1, 4.0, 0.7, false) },
        { "TopP", (0.50, 1.0, 0.95, false) },
        { "MinP", (0.01, 0.10, 0.05, false) }, // Use a floating-point scale
        { "TopK", (10, 2000, 40, true) },
        { "AI Talkativeness", (0.1, 1.0, 0.1, false) }, // Steps of 0.1
        { "AI Depth", (1, 5, 1, true) } // Steps of 1
    };

            parametersPanel.ColumnCount = 1;
            parametersPanel.RowCount = 0;
            parametersPanel.Width = 110;
            parametersPanel.ColumnStyles.Clear();
            parametersPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Add PictureBox to the top of the panel
            parametersPanel.Controls.Add(pictureBox, 0, parametersPanel.RowCount);
            parametersPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            parametersPanel.RowCount++;

            ToolTip toolTip = new ToolTip
            {
                AutoPopDelay = 5000,  // Time the tooltip remains visible
                InitialDelay = 500,   // Delay before the tooltip appears
                ReshowDelay = 500,    // Delay before reappearing
                IsBalloon = true      // Optional: makes the tooltip appear in a balloon format
            };

            bool isMouseDown = false;
            for (int i = 0; i < parameterNames.Length; i++)
            {
                int currentIndex = i;

                // Add label
                var label = new Label
                {
                    Text = parameterNames[currentIndex] + " \u2139",
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = new Font("Segoe UI", 7)
                };

                toolTip.SetToolTip(label, toolTips[currentIndex]);
                parametersPanel.Controls.Add(label, 0, parametersPanel.RowCount);
                parametersPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                parametersPanel.RowCount++;

                // Add TextBox
                var textBox = new TextBox
                {
                    Name = $"txt{parameterNames[currentIndex]}",
                    Text = defaultValues[currentIndex],
                    Dock = DockStyle.Fill,
                    TextAlign = HorizontalAlignment.Center,
                    Font = new Font("Segoe UI", 7)
                };

                parametersPanel.Controls.Add(textBox, 0, parametersPanel.RowCount);
                parametersPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                parametersPanel.RowCount++;

                // Add Slider
                if (parameterConfig.ContainsKey(parameterNames[currentIndex]))
                {
                    var (minValue, maxValue, defaultValue, isInteger) = parameterConfig[parameterNames[currentIndex]];

                    // Special handling for talkativeness
                    bool isTalkativeness = parameterNames[currentIndex] == "AI Talkativeness";
                    int scaledMin = isTalkativeness ? (int)(minValue * 10) : isInteger ? (int)minValue : (int)(minValue * 100);
                    int scaledMax = isTalkativeness ? (int)(maxValue * 10) : isInteger ? (int)maxValue : (int)(maxValue * 100);
                    int scaledDefault = isTalkativeness ? (int)(defaultValue * 10) : isInteger ? (int)defaultValue : (int)(defaultValue * 100);

                    var slider = new TrackBar
                    {
                        Name = $"slider{parameterNames[currentIndex]}",
                        Minimum = scaledMin,
                        Maximum = scaledMax,
                        Value = scaledDefault,
                        TickFrequency = isTalkativeness ? 1 : isInteger ? 1 : 10,
                        SmallChange = 1,
                        LargeChange = 1,
                        Orientation = Orientation.Horizontal,
                        Dock = DockStyle.Top,
                        Height = 30,
                        TickStyle = TickStyle.None
                    };

                    // Synchronize Slider to TextBox
                    slider.ValueChanged += (s, e) =>
                    {
                        double value = isTalkativeness
                            ? slider.Value / 10.0
                            : isInteger
                                ? slider.Value
                                : slider.Value / 100.0;

                        textBox.Text = value.ToString(isTalkativeness ? "0.0" : isInteger ? "0" : "0.00");
                    };

                    // Synchronize TextBox to Slider
                    textBox.TextChanged += (s, e) =>
                    {
                        if (double.TryParse(textBox.Text, out double value))
                        {
                            int sliderValue = isTalkativeness
                                ? (int)(value * 10)
                                : isInteger
                                    ? (int)value
                                    : (int)(value * 100);

                            if (sliderValue >= slider.Minimum && sliderValue <= slider.Maximum)
                            {
                                slider.Value = sliderValue;
                            }
                        }
                    };

                    // Handle MouseUp for specific parameters
                    slider.MouseDown += (s, e) => { isMouseDown = true; };
                    slider.MouseUp += async (s, e) =>
                    {
                        if (isMouseDown)
                        {
                            double value = isTalkativeness
                                ? slider.Value / 10.0
                                : isInteger
                                    ? slider.Value
                                    : slider.Value / 100.0;

                            if (parameterNames[currentIndex] == "AI Talkativeness")
                            {
                                string message = $"[System: talkativeness is the length of your replies, it is scaled between 0.1 and 1.0, with 1.0 being the most verbose. your talkativeness level is now {value:0.0}]";
                                AppendText("\r\n");
                                AppendText("\r\n");
                                await HandleInput(message, true);
                            }
                            else if (parameterNames[currentIndex] == "AI Depth")
                            {
                                string message = $"[System: depth is used to adjust response detail. It provides guidance on tone or complexity, where 1 is concise and 5 is highly detailed and immersive. your depth level is now {value}]";
                                AppendText("\r\n");
                                AppendText("\r\n");
                                await HandleInput(message, true);
                            }

                            isMouseDown = false; // Reset mouse tracking
                        }
                    };

                    parametersPanel.Controls.Add(slider, 0, parametersPanel.RowCount);
                    parametersPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                    parametersPanel.RowCount++;
                }
            }





            AddLinkLabel("Buy me a Beer", "https://ko-fi.com/aimultifool", 10, 18);
            AddLinkLabel("Visit us on Discord", "https://discord.gg/sCzDNAR39c", 100, 18);
        }

        private void enableCardControls()
        {
            var talkativenessSlider = parametersPanel.Controls
                .OfType<TrackBar>()
                .FirstOrDefault(t => t.Name == "sliderAI Talkativeness");

            if (talkativenessSlider != null)
            {
                talkativenessSlider.Enabled = true; // Disable the slider
            }

            var talkTxt = parametersPanel.Controls
                 .OfType<TextBox>()
                .FirstOrDefault(t => t.Name == "txtAI Talkativeness");

            if (talkTxt != null)
            {
                // talkTxt.Enabled = true; // Disable the slider
            }

            var depthSlider = parametersPanel.Controls
                .OfType<TrackBar>()
                .FirstOrDefault(t => t.Name == "sliderAI Depth");

            if (depthSlider != null)
            {
                depthSlider.Enabled = true; // Disable the slider
            }

            var depthTxt = parametersPanel.Controls
                 .OfType<TextBox>()
                 .FirstOrDefault(t => t.Name == "txtAI Depth");

            if (depthTxt != null)
            {
                // depthTxt.Enabled = true; // Disable the slider
            }
        }

        private void disableCardControls()
        {
            var talkativenessSlider = parametersPanel.Controls
    .OfType<TrackBar>()
    .FirstOrDefault(t => t.Name == "sliderAI Talkativeness");

            if (talkativenessSlider != null)
            {
                talkativenessSlider.Enabled = false; // Disable the slider
            }

            var talkTxt = parametersPanel.Controls
                 .OfType<TextBox>()
                .FirstOrDefault(t => t.Name == "txtAI Talkativeness");

            if (talkTxt != null)
            {
                talkTxt.Enabled = false; // Disable the slider
            }

            var depthSlider = parametersPanel.Controls
                .OfType<TrackBar>()
                .FirstOrDefault(t => t.Name == "sliderAI Depth");

            if (depthSlider != null)
            {
                depthSlider.Enabled = false; // Disable the slider
            }

            var depthTxt = parametersPanel.Controls
                 .OfType<TextBox>()
                 .FirstOrDefault(t => t.Name == "txtAI Depth");

            if (depthTxt != null)
            {
                depthTxt.Enabled = false; // Disable the slider
            }
        }

        private void UpdatePictureBoxImage(PictureBox pictureBox, string filePath)
        {
            // Check if pictureBox is initialized
            if (pictureBox == null)
            {
                CustomMessageBox.Show("PictureBox is not initialized.", "Error", this, isOkOnly: true);
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // Reset to the default resource image
                    if (pictureBox.Image != null)
                    {
                        pictureBox.Image.Dispose(); // Dispose the current image to free resources
                        pictureBox.Tag = null;
                    }

                    pictureBox.Image = aimultifool.Resource.aimultifool5; // Set back to resource image
                    pictureBox.Tag = "aimultifool5";
                }
                else if (File.Exists(filePath))
                {
                    // Dispose of the current image to avoid memory issues
                    if (pictureBox.Image != null)
                    {
                        pictureBox.Image.Dispose();
                    }

                    // Load and set the new image
                    pictureBox.Image = Image.FromFile(filePath);
                    pictureBox.Tag = filePath;
                }
                else
                {
                    CustomMessageBox.Show($"Image file not found: {filePath}", "Error", this, isOkOnly: true);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to update image: {ex.Message}", "Error", this, isOkOnly: true);
            }
        }


        private void AddLinkLabel(string text, string url, int offsetX, int offsetY)
        {


            // Initialize the LinkLabel
            var linkLabel = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                TabStop = false,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right, // Anchor to bottom-right
                Margin = new Padding(0), // Optional: Adjust margin as needed
                LinkColor = Color.Black,            // Set link color to black
                ActiveLinkColor = Color.Black,      // Set active link color to black
                VisitedLinkColor = Color.Black,     // Set visited link color to black
                DisabledLinkColor = Color.Black     // Set disabled link color to black

            };

            linkLabel.TabStop = false;

            // Position the LinkLabel manually at the specified offset
            linkLabel.Location = new Point(this.ClientSize.Width - linkLabel.PreferredWidth - offsetX,
                                           this.ClientSize.Height - linkLabel.Height - offsetY);

            linkLabel.LinkClicked += (s, e) =>
            {
                // Open the URL on left-click
                if (e.Button == MouseButtons.Left)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            };

            // Ensure the LinkLabel updates its position when the form is resized
            linkLabel.SizeChanged += (s, e) =>
            {
                linkLabel.Location = new Point(this.ClientSize.Width - linkLabel.PreferredWidth - offsetX,
                                               this.ClientSize.Height - linkLabel.Height - offsetY);
            };
            this.Resize += (s, e) =>
            {
                linkLabel.Location = new Point(this.ClientSize.Width - linkLabel.PreferredWidth - offsetX,
                                               this.ClientSize.Height - linkLabel.Height - offsetY);
            };


            // Add the LinkLabel to the form's controls
            Controls.Add(linkLabel);
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            cancellationTokenSource?.Cancel();
            //AppendText("\r\nResponse interrupted. You can now enter a new message.\r\n");
            cancellationTokenSource = new CancellationTokenSource();
            foreach (var menu in customMenus)
            {
                menu.Enabled = true;

            }

            btnStop.Enabled = false; // Disable the Stop button after it's pressed
        }

        private void BtnModify_Click(object sender, EventArgs e)
        {
            if (_modifierForm == null || _modifierForm.IsDisposed)
            {
                // Create a new instance if none exists or the previous one was disposed
                _modifierForm = new ModifierForm(this);

                // Ensure StartPosition is Manual
                _modifierForm.StartPosition = FormStartPosition.Manual;

                // Forcefully calculate dimensions after initializing the form
                _modifierForm.Load += (s, args) =>
                {
                    // Calculate the screen bounds of txtConsole
                    var txtConsoleScreenBounds = txtConsole.RectangleToScreen(txtConsole.ClientRectangle);

                    // Align the top and right edges of the ModifierForm with txtConsole
                    int formLeft = txtConsoleScreenBounds.Right - _modifierForm.Width; // Align right edge
                    int formTop = txtConsoleScreenBounds.Top + 8; // Align top edge

                    // Ensure the form doesn't go off-screen
                    var screenBounds = Screen.FromControl(this).WorkingArea;
                    formLeft = Math.Max(screenBounds.Left, Math.Min(formLeft, screenBounds.Right - _modifierForm.Width));
                    formTop = Math.Max(screenBounds.Top, Math.Min(formTop, screenBounds.Bottom - _modifierForm.Height));

                    // Assign the calculated position
                    _modifierForm.Location = new Point(formLeft, formTop);
                };

                // Show the form
                _modifierForm.Show();
            }
            else
            {
                // If the form is hidden, make it visible
                if (!_modifierForm.Visible)
                {
                    _modifierForm.Visible = true;
                }

                // Bring the form to the front
                _modifierForm.BringToFront();
            }
        }

        private void BtnInjectJSON_Click(object sender, EventArgs e)
        {
            try
            {
                if (detectedJson == null)
                {
                    CustomMessageBox.Show("No valid JSON object found in the console.", "Error", this, isOkOnly: true);
                    return;
                }

                string pngFilePath = SelectPngFile();
                if (string.IsNullOrEmpty(pngFilePath))
                {
                    return; // User canceled file dialog
                }

                string v2JSON = AddDataBlock(detectedJson);

                string base64CharacterData = ConvertJsonToBase64(v2JSON);
                if (string.IsNullOrEmpty(base64CharacterData))
                {
                    return; // Error message already shown
                }

                if (EmbedJsonInPng(pngFilePath, base64CharacterData))
                {
                    CustomMessageBox.Show($"JSON data successfully embedded into the PNG at:\n{pngFilePath}", "Success", this, isOkOnly: true);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", this, isOkOnly: true);
            }
        }

        private void txtConsole_TextChanged(object sender, EventArgs e)
        {
            // Attempt to detect JSON in real-time
            TryDetectAndStoreJson();
        }


        private string SelectPngFile()
        {
            using (OpenFileDialog openPngDialog = new OpenFileDialog
            {
                Filter = "PNG Files (*.png)|*.png",
                Title = "Select a PNG Image to Embed JSON"
            })
            {
                if (openPngDialog.ShowDialog() == DialogResult.OK)
                {
                    return openPngDialog.FileName;
                }
            }

            // User canceled the dialog
            return null;
        }

        private string ConvertJsonToBase64(string json)
        {
            try
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error encoding JSON: {ex.Message}", "Error", this, isOkOnly: true);
                return null;
            }
        }

        private bool EmbedJsonInPng(string pngFilePath, string base64CharacterData)
        {
            try
            {
                using (var image = new MagickImage(pngFilePath))
                {
                    image.SetAttribute("chara", base64CharacterData); // Embed Base64 JSON as metadata
                    image.Write(pngFilePath); // Overwrite the selected PNG
                }

                return true; // Successfully embedded JSON
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"An error occurred while embedding JSON data: {ex.Message}", "Error", this, isOkOnly: true);
                return false;
            }
        }

        private async void BtnContinue_Click(object sender, EventArgs e)
        {
            // Add a separator or some indication for continuation
            AppendText("\r\n");
            AppendText("\r\n");

            try
            {
                // Send "continue" to the AI session
                await HandleInput("continue", true);
            }
            catch (Exception ex)
            {
                AppendText($"Error while continuing AI: {ex.Message}\r\n");
            }
        }

        private async void BtnRestartCard_Click(object sender, EventArgs e)
        {

            btnRestartCard.Enabled = false;
            txtConsole.Clear();
            txtUserInput.Clear();
            ReloadInferenceParams();

            AppendText("AI is processing Character Card please wait..\r\n");

            clearContext();

            StartNewChat();

            TextBox txtTalkativeness = parametersPanel.Controls["txtAI Talkativeness"] as TextBox;
            TextBox txtDepthPrompt = parametersPanel.Controls["txtAI Depth"] as TextBox;

            txtTalkativeness.Text = talk;
            txtDepthPrompt.Text = depth;


            AppendText("\r\n");

            if (string.IsNullOrEmpty(jsonRestartCard))
            {
                CustomMessageBox.Show("JSON content is not initialized.", "Warning", this, isOkOnly: true);
                return;
            }

            // Handle the decoded JSON content
            await HandleInput(jsonRestartCard, true);
        }
        private void BtnRewind_Click(object sender, EventArgs e)
        {
            if (chatHistory.Count > 1) // Ensure at least one user input and AI reply remain
            {
                // Remove the last user input and AI response from the chat history
                chatHistory.RemoveAt(chatHistory.Count - 1);

                // Copy current context to buffer and clear it
                clearContext(keepCardImageOpen: true);

                // Remove the last pair of user input and AI response from the buffer
                if (contextBuffer.Count > 2) // Ensure at least the first interaction remains
                {
                    ModifyContextBuffer(2); // Remove the last user + AI interaction (2 messages)
                }

                // Restore the modified buffer to the session and update the console
                RestoreContext();


                AppendWarningToConsole("Rewind successful.", txtConsole.ForeColor);
                RewindTokenCount();

            }
            else
            {
                AppendWarningToConsole("Cannot rewind further.", txtConsole.ForeColor);
            }
        }


        //hghgffhg
        private async void BtnClear_Click(object sender, EventArgs e)
        {
            // Display confirmation dialog
            DialogResult result = CustomMessageBox.Show(
                "Are you sure you want to start a new chat session?\r\nThis will clear all current data.",
                "Confirm New Chat",
                this,
                isOkOnly: false // Allows "Yes" and "No" buttons
            );

            // Check user response
            if (result != DialogResult.Yes)
            {
                return; // Cancel the operation if the user chooses "No"
            }

            UpdatePictureBoxImage(pictureBox, null); // Use null or empty string as a signal to reset


            txtConsole.Clear();

            btnClear.Enabled = false;
            btnRewind.Enabled = false;
            btnContinue.Enabled = false;

            AppendText("New chat session started..");

            TextBox txtTemperature = parametersPanel.Controls["txtTemperature"] as TextBox;
            txtTemperature.Text = "0.7";
            TextBox txtTopP = parametersPanel.Controls["txtTopP"] as TextBox;
            txtTopP.Text = "0.95";
            TextBox txtMinP = parametersPanel.Controls["txtMinP"] as TextBox;
            txtMinP.Text = "0.05";
            TextBox txtTopK = parametersPanel.Controls["txtTopK"] as TextBox;
            txtTopK.Text = "40";
            TextBox txtMaxTokens = parametersPanel.Controls["txtMaxTokens"] as TextBox;
            txtMaxTokens.Text = "-1";
            TextBox txtRepeatPenalty = parametersPanel.Controls["txtRepeatPenalty"] as TextBox;
            txtRepeatPenalty.Text = "1.1";
            ReloadInferenceParams();

            resetSliders();

            clearContext();

            var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");

            // If the form is found and we're not instructed to keep it open, close it
            if (openForm != null)
            {
                openForm.Close();
            }

            txtUserInput.Focus();

            loadedfromCard = false;
            btnRestartCard.Enabled = loadedfromCard;

            StartNewChat();

        }

        private void resetSliders()
        {
            TextBox txtTalkativeness = parametersPanel.Controls["txtAI Talkativeness"] as TextBox;


            var talkativenessSlider = parametersPanel.Controls
                .OfType<TrackBar>()
                .FirstOrDefault(t => t.Name == "sliderAI Talkativeness");

            if (talkativenessSlider != null)
            {
                talkativenessSlider.Value = talkativenessSlider.Minimum; // Disable the slider
            }

            var depthSlider = parametersPanel.Controls
                 .OfType<TrackBar>()
                  .FirstOrDefault(t => t.Name == "sliderAI Depth");

            if (depthSlider != null)
            {
                depthSlider.Value = depthSlider.Minimum; // Disable the slider
            }

            if (txtTalkativeness != null)
            {
                txtTalkativeness.Text = string.Empty; // Clear the TextBox value
            }
            TextBox txtDepth = parametersPanel.Controls["txtAI Depth"] as TextBox;
            if (txtDepth != null)
            {
                txtDepth.Text = string.Empty; // Clear the TextBox value
            }

        }
        private void TxtConsole_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                // Attempt to open the link in the default browser
                Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Could not open link: {ex.Message}", "Error", this, isOkOnly: true);

            }
        }

        private async void TxtUserInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Suppress the "ding" sound for all keys when RichTextBox is ReadOnly
            if (txtUserInput.ReadOnly)
            {
                e.SuppressKeyPress = true; // Suppress the key press to prevent the "ding" sound
                return;
            }

            // Check if Enter or Shift + Enter is pressed
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Prevent the default Enter key behavior

                // Trigger BtnStop_Click
                BtnStop_Click(btnStop, EventArgs.Empty);

                // Wait for btnStop to be disabled
                await WaitForButtonToBeDisabled(btnStop);

                // Wait until the previous input handling is complete
                while (isProcessingInput)
                {
                    await Task.Delay(100); // Polling with a small delay
                }

                // Submit the text
                string input = txtUserInput.Text.Trim();
                txtUserInput.Clear();

                // If input is empty, submit "continue"
                if (string.IsNullOrEmpty(input))
                {
                    AppendText("\r\n");
                    AppendText("\r\n");
                    await HandleInput("continue", true);
                }
                else

                    await HandleInput(input);

            }
            // Check if Shift + Enter is pressed
            else if (e.KeyCode == Keys.Enter && e.Shift)
            {
                e.SuppressKeyPress = true;

                // Add a newline in the TextBox
                int caretPosition = txtUserInput.SelectionStart;
                txtUserInput.Text = txtUserInput.Text.Insert(caretPosition, Environment.NewLine);
                txtUserInput.SelectionStart = caretPosition + Environment.NewLine.Length; // Move caret after the newline
            }
        }

        private async Task PromptUserToSelectModel()
        {
            btnStop.Enabled = false;
            btnContinue.Enabled = false;
            btnClear.Enabled = false;
            menuStrip1.Enabled = false;
            btnRewind.Enabled = false;

            AppendText("Searching for available GGUF models in 'models-gguf' folder...\r\n");
            string modelsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf");
            var modelFiles = Directory.GetFiles(modelsFolder, "*.gguf").Select(Path.GetFileName).ToArray();

            AppendText("\r\n");

            if (modelFiles.Length == 0)
            {
                AppendText("No models found in the 'models-gguf' folder.\r\n");
                AppendText("\r\n");
                AppendText("Would you like to download the default roleplay model? (y/n): ");
                txtConsole.Focus();

                bool userConfirmed = await WaitForYesNoInputAsync(); // Method to handle y/n input
                if (userConfirmed)
                {
                    AppendText("\r\n\nDownloading default model...\r\n");
                    string defaultModelUrl = "https://huggingface.co/Lewdiculous/L3-8B-Stheno-v3.2-GGUF-IQ-Imatrix/resolve/main/L3-8B-Stheno-v3.2-Q4_K_M-imat.gguf?download=true"; // Replace with actual URL
                    string defaultModelPath = Path.Combine(modelsFolder, "Card Roleplay Uncensored L3-8B-Stheno-v3.2-Q4_K_M-imat.gguf");
                    await DownloadModelAsync(defaultModelUrl, defaultModelPath);
                    AppendText("\r\nDefault model downloaded successfully.\r\n\n");

                    // Refresh the list of available models after downloading
                    modelFiles = Directory.GetFiles(modelsFolder, "*.gguf").Select(Path.GetFileName).ToArray();
                }
                else
                {
                    AppendText("\r\n\nYou must either download the default model or put your own in the 'models-gguf' folder\r\n\n");
                    AppendText("\r\n");

                    await PromptUserToSelectModel(); // Restart the procedure
                    return;
                }
            }

            AppendText("Please select a model by pressing the corresponding number key.\r\nSet the GpuLayerCount and Context Size if you wish before selection as they can only be set before a model loads:\r\n\n");

            for (int i = 0; i < modelFiles.Length; i++)
            {
                AppendText($"{i + 1}: {modelFiles[i]}\r\n");
            }

            txtConsole.Focus();

            async void HandleModelSelectionKeyPress(object sender, KeyPressEventArgs e)
            {
                e.Handled = true; // Suppress the ding sound for all key presses

                if (char.IsDigit(e.KeyChar))
                {
                    int selectedNumber = int.Parse(e.KeyChar.ToString());
                    if (selectedNumber >= 1 && selectedNumber <= modelFiles.Length)
                    {
                        txtConsole.KeyPress -= HandleModelSelectionKeyPress; // Remove the event handler
                        selectedModelPath = Path.Combine(modelsFolder, modelFiles[selectedNumber - 1]);
                        AppendText("\r\n");
                        AppendText($"Selected model: {Path.GetFileName(selectedModelPath)}\r\n");
                        await StartSession(); // Load the selected model
                    }
                    else
                    {
                        AppendText("Invalid selection. Please select a valid model number.\r\n");
                    }
                }
                else
                {
                    AppendText("Invalid input. Please select a model by number.\r\n");
                }
            }

            txtConsole.KeyPress += HandleModelSelectionKeyPress;
        }

        private async Task<bool> WaitForYesNoInputAsync()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            txtConsole.KeyPress += (sender, e) =>
            {
                if (e.KeyChar == 'y' || e.KeyChar == 'Y')
                {
                    e.Handled = true; // Suppress the beep
                    tcs.TrySetResult(true);
                }
                else if (e.KeyChar == 'n' || e.KeyChar == 'N')
                {
                    e.Handled = true; // Suppress the beep
                    tcs.TrySetResult(false);
                }
                else
                {
                    e.Handled = true; // Suppress invalid keypresses
                }
            };

            return await tcs.Task;
        }

        private async Task DownloadModelAsync(string modelUrl, string modelPath)
        {
            try
            {
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;
                        var lastUpdateTime = DateTime.Now;

                        // Set up a token source for resetting timeout dynamically
                        var timeoutTokenSource = new CancellationTokenSource();
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(20), timeoutTokenSource.Token);

                        while (true)
                        {
                            // Start reading a chunk with the cancellation token
                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, timeoutTokenSource.Token);
                            bytesRead = await Task.WhenAny(readTask, timeoutTask) == readTask
                                ? await readTask
                                : throw new OperationCanceledException("Download timed out due to inactivity.");

                            if (bytesRead == 0) // End of the stream
                                break;

                            // Reset the timeout timer after progress is made
                            timeoutTokenSource.Cancel();
                            timeoutTokenSource = new CancellationTokenSource();
                            timeoutTask = Task.Delay(TimeSpan.FromSeconds(20), timeoutTokenSource.Token);

                            // Write the downloaded chunk to the file
                            await fileStream.WriteAsync(buffer, 0, bytesRead, timeoutTokenSource.Token);
                            totalRead += bytesRead;

                            // Calculate percentage and convert bytes to MB
                            double progressPercentage = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;
                            double totalReadMB = totalRead / (1024.0 * 1024.0);
                            double totalMB = totalBytes / (1024.0 * 1024.0);

                            // Update progress every 5 seconds
                            if ((DateTime.Now - lastUpdateTime).TotalSeconds >= 5)
                            {
                                AppendText($"Downloaded {totalReadMB:F2} MB / {totalMB:F2} MB ({progressPercentage:F2}% complete)...\r\n");
                                lastUpdateTime = DateTime.Now;
                            }
                        }

                        AppendText("\r\n");
                        AppendText("Download Complete.\r\n");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                CustomMessageBox.Show("Download was canceled due to inactivity or timeout.", "Error", this, isOkOnly: true);

                // Delete the partially downloaded file if it exists
                if (File.Exists(modelPath))
                {
                    try
                    {
                        File.Delete(modelPath);
                    }
                    catch (Exception deleteEx)
                    {
                        CustomMessageBox.Show($"Failed to clean up the partially downloaded file: {deleteEx.Message}", "Error", this, isOkOnly: true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Notify the user of other errors
                CustomMessageBox.Show($"Failed to download model: {ex.Message}", "Error", this, isOkOnly: true);

                // Delete the partially downloaded file if it exists
                if (File.Exists(modelPath))
                {
                    try
                    {
                        File.Delete(modelPath);
                    }
                    catch (Exception deleteEx)
                    {
                        CustomMessageBox.Show($"Failed to clean up the partially downloaded file: {deleteEx.Message}", "Error", this, isOkOnly: true);
                    }
                }
            }
        }

        private async void HandleModelSelectionKeyPress(object sender, KeyPressEventArgs e)
        {
            string modelsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf");
            var modelFiles = Directory.GetFiles(modelsFolder, "*.gguf").Select(Path.GetFileName).ToArray();

            // Check if the pressed key is a digit
            if (char.IsDigit(e.KeyChar))
            {
                int modelIndex = int.Parse(e.KeyChar.ToString());
                if (modelIndex > 0 && modelIndex <= modelFiles.Length)
                {
                    // Valid selection
                    e.Handled = true; // Suppress the beep

                    // Remove the KeyPress event handler
                    txtConsole.KeyPress -= HandleModelSelectionKeyPress;

                    // Select the model
                    selectedModelPath = Path.Combine(modelsFolder, modelFiles[modelIndex - 1]);
                    AppendText($"\r\nSelected model: {Path.GetFileName(selectedModelPath)}\r\n");

                    // Start the session
                    await StartSession();
                    return;
                }
            }

            // Suppress the beep even for invalid input
            e.Handled = true;
            AppendText("\r\nInvalid selection. Please press a valid number key.\r\n");
        }


        private void LoadModel()
        {
            try
            {
                // Dispose of previous model and context if they exist
                model?.Dispose();
                context?.Dispose();
                model = null;
                context = null;

                // Parse parameters
                int gpuLayerCount = int.Parse(GetParameterValue("GpuLayerCount"));
                int contextSize = int.Parse(GetParameterValue("ContextSize"));
                float temperature = float.Parse(GetParameterValue("Temperature"));
                float topK = float.Parse(GetParameterValue("TopK"));
                float topP = float.Parse(GetParameterValue("TopP"));
                float minP = float.Parse(GetParameterValue("MinP"));
                int maxTokens = int.Parse(GetParameterValue("MaxTokens"));
                float repeatPenalty = float.Parse(GetParameterValue("RepeatPenalty"));

                // Set model parameters
                var parameters = new ModelParams(selectedModelPath)
                {
                    GpuLayerCount = gpuLayerCount,
                    ContextSize = (uint)contextSize
                };

                // Load the model and context
                model = LLamaWeights.LoadFromFile(parameters); // Initialize the model
                context = model.CreateContext(parameters); // Ensure context is initialized
                var executor = new InteractiveExecutor(context);
                session = new ChatSession(executor, new ChatHistory());
                session.WithHistoryTransform(new PromptTemplateTransformer(model, withAssistant: true));
                session.WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
                    new[] { model.Tokens.EndOfTurnToken ?? "User:", "�", "im_end", "<|" }, 10));

                inferenceParams = new InferenceParams
                {
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = temperature,
                        TopK = (int)topK,
                        TopP = topP,
                        MinP = minP,
                        RepeatPenalty = repeatPenalty,
                        Seed = (uint)Math.Abs(RandomSeedGenerator.GenerateSeed())
                    },
                    MaxTokens = maxTokens,
                    AntiPrompts = new[] { model.Tokens.EndOfTurnToken ?? "User:", "im_end", "<|" }
                };

            }

            catch (Exception)
            {

                AppendText("\r\nTHE MODEL IS INVALID OR CORRUPT. Please delete it and restart aiMultiFool.\r\n");

            }
        }

        private async Task StartSession()
        {
            if (isLoading || string.IsNullOrEmpty(selectedModelPath))
            {
                AppendText("Session is already loading or no model was selected.\r\n");
                return;
            }

            isLoading = true;
            try
            {

                AppendText("\nLoading parameters..\r\n");
                AppendText("\r\n");
                AppendText($"GpuLayerCount: {GetParameterValue("GpuLayerCount")}\r\n");
                AppendText($"ContextSize: {GetParameterValue("ContextSize")}\r\n");
                AppendText($"Temperature: {GetParameterValue("Temperature")}\r\n");
                AppendText($"TopK: {GetParameterValue("TopK")}\r\n");
                AppendText($"TopP: {GetParameterValue("TopP")}\r\n");
                AppendText($"MinP: {GetParameterValue("MinP")}\r\n");
                AppendText($"MaxTokens: {GetParameterValue("MaxTokens")}\r\n");
                AppendText($"RepeatPenalty: {GetParameterValue("RepeatPenalty")}\r\n");
                AppendText("\r\n");
                AppendText("Loading model...\r\n");
                await Task.Run(() => LoadModel());

                AppendText("\nChat session started..");

                txtUserInput.Enabled = true;
                txtUserInput.Focus();

                btnStop.Enabled = false;
                //btnContinue.Enabled = true;
                txtUserInput.ReadOnly = false;
                //btnClear.Enabled = true;
                //btnRewind.Enabled = true;
                menuStrip1.Enabled = true;


                TextBox txtGPU = parametersPanel.Controls["txtGpuLayerCount"] as TextBox;
                TextBox txtContext = parametersPanel.Controls["txtContextSize"] as TextBox;
                txtGPU.Enabled = false;
                txtContext.Enabled = false;

                enableCardControls();


            }
            catch (Exception ex)
            {
                AppendText($"Error starting session: {ex.Message}\r\n");
            }
            finally
            {
                isLoading = false;
            }
        }

        public void ReloadInferenceParams()
        {
            try
            {
                // Collect values from textboxes (or other sources)
                float temperature = float.Parse(GetParameterValue("Temperature"));
                float topK = float.Parse(GetParameterValue("TopK"));
                float topP = float.Parse(GetParameterValue("TopP"));
                float minP = float.Parse(GetParameterValue("MinP"));
                int maxTokens = int.Parse(GetParameterValue("MaxTokens"));
                float repeatPenalty = float.Parse(GetParameterValue("RepeatPenalty"));

                // Update the inference parameters
                inferenceParams = new InferenceParams
                {
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = temperature,
                        TopK = (int)topK,
                        TopP = topP,
                        MinP = minP,
                        RepeatPenalty = repeatPenalty,
                        Seed = (uint)Math.Abs(RandomSeedGenerator.GenerateSeed())
                    },
                    MaxTokens = maxTokens,
                    AntiPrompts = inferenceParams.AntiPrompts // Retain anti-prompts from previous config
                };

            }
            catch (Exception ex)
            {
                AppendText($"Error reloading inference parameters: {ex.Message}\r\n");
            }
        }


        private string GetParameterValue(string parameterName)
        {
            // Get the textbox with the specified name and return its value
            var textBox = parametersPanel.Controls
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == $"txt{parameterName}");

            return textBox?.Text ?? string.Empty; // Return empty string if the textbox is not found
        }

        public async Task HandleInput(string userInput, bool omitInitialMessages = false)
        {
            if (isProcessingInput) return; // Prevent re-entry

            isProcessingInput = true; // Set the flag to indicate processing has started

            // Disable the input box while AI is processing
            btnStop.Enabled = true;
            btnRewind.Enabled = false;
            btnContinue.Enabled = false;
            btnClear.Enabled = false;
            menuStrip1.Enabled = false;
            btnRestartCard.Enabled = false;

            if (pictureBox.Tag as string == "aimultifool5")
            {
                pictureBox.Image = aimultifool.Resource.aimultifool5open; // Set to open image
                pictureBox.Tag = "aimultifool5open";
            }

            cancellationTokenSource = new CancellationTokenSource();
            string aiResponse = "";

            if (!omitInitialMessages)
            {
                AppendText($"\r\n\n{userInput}\r\n\n", isUserInput: true); // this is where the > used to be
            }

            try
            {
                StartTokenTracking();

                await foreach (var text in session.ChatAsync(
                    new ChatHistory.Message(AuthorRole.User, userInput), inferenceParams, cancellationTokenSource.Token))
                {
                    TrackToken(text);
                    AppendText(text); // Real-time UI updates
                    aiResponse += text; // Accumulate AI response

                    // Update the UI with token stats
                    UpdateTokenStatsUI();
                }

                // Store the user input and AI response in the chat history
                chatHistory.Add(new Tuple<string, string>(userInput, aiResponse));
            }
            catch (OperationCanceledException)
            {
                AppendText("\r\nResponse interrupted. You can now enter a new message.\r\n");
            }
            finally
            {
                // Stop the timer when AI stops processing
                StopTokenTracking();

                // Re-enable input and UI elements
                btnStop.Enabled = false;
                btnContinue.Enabled = true;
                btnModify.Enabled = true;
                btnClear.Enabled = true;
                btnRewind.Enabled = true;
                txtUserInput.Focus();
                menuStrip1.Enabled = true;

                if (pictureBox.Tag as string == "aimultifool5open")
                {
                    pictureBox.Image = aimultifool.Resource.aimultifool5; // Set to open image
                    pictureBox.Tag = "aimultifool5";
                }


                isProcessingInput = false; // Clear the processing flag

                btnRestartCard.Enabled = loadedfromCard;

                // Final update to token stats after processing is complete
                UpdateTokenStatsUI(finalUpdate: true);
            }
        }

        private void StopTokenTracking()
        {
            if (titleUpdateTimer != null)
            {
                titleUpdateTimer.Stop();
                titleUpdateTimer.Dispose();
                titleUpdateTimer = null;
            }
            stopwatch.Stop();
        }

        private async Task WaitForButtonToBeDisabled(Button button)
        {
            // TaskCompletionSource to signal when the button is disabled
            var tcs = new TaskCompletionSource();

            // Monitor the button's Enabled property
            void ButtonEnabledChangedHandler(object? sender, EventArgs e)
            {
                if (!button.Enabled) // Check if the button is disabled
                {
                    button.EnabledChanged -= ButtonEnabledChangedHandler; // Unsubscribe
                    tcs.SetResult(); // Signal completion
                }
            }

            button.EnabledChanged += ButtonEnabledChangedHandler; // Subscribe to the event

            // If the button is already disabled, complete immediately
            if (!button.Enabled)
            {
                button.EnabledChanged -= ButtonEnabledChangedHandler; // Unsubscribe
                tcs.SetResult();
            }

            await tcs.Task; // Await the TaskCompletionSource
        }

        private void StartTokenTracking()
        {
            tokenCount = 0;
            llamaTimings = new LLamaTimings(0);
            peakTokensPerSecond = 0;
            maxVisibleTPS = 50; // Initial scaling value

            stopwatch = Stopwatch.StartNew();

            titleUpdateTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            titleUpdateTimer.Tick += TitleUpdateTimer_Tick;
            titleUpdateTimer.Start();

            InitializeGraphBitmap();
        }


        private void TrackToken(string text)
        {
            int tokenCountForText = CountTokens(text); // Count tokens in the input
            tokenCount += tokenCountForText;

            // Update LLamaTimings
            llamaTimings = llamaTimings.IncrementTokensSampled(tokenCountForText);
        }

        private int CountTokens(string text)
        {
            // Split text into tokens by whitespace
            var tokens = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length;
        }


        private void UpdateTokenStatsUI(bool finalUpdate = false)
        {
            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double tokensPerSecond = (elapsedSeconds > 0) ? tokenCount / elapsedSeconds : 0;

            if (tokensPerSecond > peakTokensPerSecond)
            {
                peakTokensPerSecond = tokensPerSecond;
            }

            if (finalUpdate)
            {
                // Store the token count in the list for rewinding and update the session total
                sessionTokenHistory.Add(tokenCount);
                totalSessionTokens += tokenCount;
            }

            // Calculate percentage of context used
            if (contextSize == 0)
            {
                // Set the context size if not already set
                contextSize = uint.Parse(GetParameterValue("ContextSize"));
            }

            double contextUsedPercentage = (contextSize > 0)
                ? (double)totalSessionTokens / contextSize * 100
                : 0;

            // Check if the percentage exceeds 100% and warning has not been fired
            if (finalUpdate && contextUsedPercentage > 100 && !warningFired)
            {
                // Write warning to txtConsole in bold
                AppendWarningToConsole("Warning: ContextSize exceeded 100%! AI will now start forgetting start of chat session.", txtConsole.ForeColor);
                warningFired = true; // Prevent the warning from being fired again
            }

            Action updateUI = () =>
            {
                // Update label with TPS, Total Tokens as % of context size, and Peak TPS
                tpsLabel.Text = finalUpdate
                    ? $"{peakTokensPerSecond:0.0} peak tok/s •" +
                    $" {tokenCount} tokens • {contextUsedPercentage:0.0}% of context used"
                    : $"{tokensPerSecond:0.0} tok/sec • {tokenCount} tokens";

                if (!finalUpdate)
                {
                    UpdateGraph(tokensPerSecond); // Update graph in real time
                }
            };

            if (this.InvokeRequired)
            {
                this.Invoke(updateUI);
            }
            else
            {
                updateUI();
            }
        }

        // Method to append warning in bold to the txtConsole rich text box
        private void AppendWarningToConsole(string message, Color textColor)
        {
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.SelectionLength = 0;

            // Set bold style
            txtConsole.SelectionFont = new Font(txtConsole.Font, FontStyle.Bold);
            txtConsole.SelectionColor = textColor;

            // Append the warning message
            txtConsole.AppendText(Environment.NewLine + Environment.NewLine + message);
            SendMessage(txtConsole.Handle, WM_VSCROLL, SB_PAGEBOTTOM, IntPtr.Zero);

            // Reset font style and color
            txtConsole.SelectionFont = new Font(txtConsole.Font, FontStyle.Regular);
            txtConsole.SelectionColor = txtConsole.ForeColor;
        }


        public void RewindTokenCount()
        {
            if (sessionTokenHistory.Count > 0)
            {
                // Remove the last token count from history and update totalSessionTokens
                int lastTokenCount = sessionTokenHistory[^1]; // Get the last token count
                sessionTokenHistory.RemoveAt(sessionTokenHistory.Count - 1); // Remove it from the list
                totalSessionTokens -= lastTokenCount; // Adjust the total session tokens

                // Recalculate the percentage of context used
                double contextUsedPercentage = (contextSize > 0)
                    ? (double)totalSessionTokens / contextSize * 100
                    : 0;

                // Reset the warning flag if context usage drops below 100%
                if (contextUsedPercentage < 100)
                {
                    warningFired = false;
                }

                // Update the label with the recalculated percentage
                tpsLabel.Text = $"{peakTokensPerSecond:0.0} peak tok/s • {tokenCount} tokens • {contextUsedPercentage:0.0}% of context used";
            }
        }


        private void AdjustMaxVisibleTPS(double tokensPerSecond)
        {
            // Ensure maxVisibleTPS dynamically adjusts to tokensPerSecond with smooth scaling
            double minScale = 10; // Minimum scale to ensure reasonable graphing

            if (tokensPerSecond > maxVisibleTPS)
            {
                // Increase the maximum scale with a buffer to prevent abrupt changes
                maxVisibleTPS = tokensPerSecond * 1.2; // 20% buffer for smoother scaling
            }
            else if (tokensPerSecond < maxVisibleTPS * 0.5 && maxVisibleTPS > minScale)
            {
                // Decrease the maximum scale when tokensPerSecond is significantly lower
                maxVisibleTPS = Math.Max(minScale, maxVisibleTPS * 0.8); // Smooth decrease with a 20% reduction
            }

            // Ensure maxVisibleTPS never drops below the minimum scale
            maxVisibleTPS = Math.Max(maxVisibleTPS, minScale);
        }

        private void InitializeGraphPictureBox()
        {
            // Create the Bitmap for the PictureBox
            graphBitmap = new Bitmap(graphPictureBox.Width, graphPictureBox.Height);

            // Clear the Bitmap with the tpsLabel's BackColor
            using (Graphics g = Graphics.FromImage(graphBitmap))
            {
                g.Clear(txtConsole.BackColor);
            }

            // Assign the Bitmap to the PictureBox
            graphPictureBox.Image = graphBitmap;
        }

        private void UpdateGraph(double tokensPerSecond)
        {
            // Check if the tokensPerSecond value is valid (non-zero or above a threshold)
            if (tokensPerSecond <= 0)
            {
                return; // Do not update the graph
            }

            // Adjust the visible TPS scale dynamically
            AdjustMaxVisibleTPS(tokensPerSecond);

            // Scale tokensPerSecond to fit graph height
            int graphHeight = graphPictureBox.Height;
            int yValue = (int)((tokensPerSecond / maxVisibleTPS) * (graphHeight * 0.9)); // Use 90% of the height for scaling
            yValue = graphHeight - Math.Max(0, Math.Min(graphHeight - 1, yValue)); // Invert Y-axis for drawing

            using (Brush backgroundBrush = new SolidBrush(txtConsole.BackColor))
            using (Brush graphBrush = new SolidBrush(txtConsole.ForeColor))
            using (Brush fillBrush = new SolidBrush(Color.FromArgb(50, txtConsole.ForeColor))) // Semi-transparent brush
            using (Graphics g = Graphics.FromImage(graphBitmap))
            {
                if (xPosition < graphPictureBox.Width)
                {
                    // Fill the area under the graph
                    if (xPosition > 0)
                    {
                        Point[] fillPolygon = {
                    new Point(xPosition - 1, previousYValue),
                    new Point(xPosition, yValue),
                    new Point(xPosition, graphHeight),
                    new Point(xPosition - 1, graphHeight)
                };
                        g.FillPolygon(fillBrush, fillPolygon);
                    }

                    // Draw a point at the current position
                    g.FillRectangle(graphBrush, xPosition, yValue, 2, 2);
                }
                else
                {
                    // Scroll the graph leftward by one pixel
                    g.DrawImage(graphBitmap, -1, 0);

                    // Fill the rightmost column with the background color
                    g.FillRectangle(backgroundBrush, graphBitmap.Width - 1, 0, 1, graphHeight);

                    // Fill the area under the graph
                    Point[] fillPolygon = {
                new Point(graphBitmap.Width - 2, previousYValue),
                new Point(graphBitmap.Width - 1, yValue),
                new Point(graphBitmap.Width - 1, graphHeight),
                new Point(graphBitmap.Width - 2, graphHeight)
            };
                    g.FillPolygon(fillBrush, fillPolygon);

                    // Draw the new graph point at the right edge
                    g.FillRectangle(graphBrush, graphBitmap.Width - 1, yValue, 2, 2);
                }
            }

            // Update the previous Y-value for the next iteration
            previousYValue = yValue;

            xPosition++;

            if (xPosition >= graphPictureBox.Width)
            {
                // Reset the x-position when it reaches the width
                xPosition = graphPictureBox.Width;
            }

            // Update the PictureBox with the new graph
            graphPictureBox.Image = graphBitmap;
        }

        private void StartNewChat()
        {
            // Reset session token history and total tokens
            sessionTokenHistory.Clear();
            totalSessionTokens = 0;

            // Reset token count
            tokenCount = 0;

            // Reset TPS and peak TPS
            peakTokensPerSecond = 0;

            // Reset the UI label
            tpsLabel.Text = "0.0 tok/sec • 0 tokens • 0.0% of context used";

            // Clear the graph
            ClearGraph();

            if (_modifierForm != null && !_modifierForm.IsDisposed)
            {
                _modifierForm.Close();
                _modifierForm = null; // Optional: Nullify the reference to indicate it's closed
            }


        }

        private void ClearGraph()
        {
            // Reset graph-related variables
            xPosition = 0;
            previousYValue = 0;

            // Clear the graph bitmap
            using (Graphics g = Graphics.FromImage(graphBitmap))
            {
                g.Clear(txtConsole.BackColor); // Fill the bitmap with the background color
            }

            // Update the PictureBox to show the cleared graph
            graphPictureBox.Image = graphBitmap;
        }


        private void AppendText(string text, bool isUserInput = false)
        {
            Action appendAction = () =>
            {
                // Set the text style based on whether it's user input or AI response
                if (isUserInput)
                {
                    ReloadInferenceParams();
                    txtConsole.SelectionStart = txtConsole.TextLength;
                    txtConsole.SelectionLength = 0;

                    // Apply bold formatting to the new text
                    txtConsole.SelectionFont = new Font(txtConsole.Font, FontStyle.Bold);

                    // Add the text and a newline
                    //txtConsole.SelectedText = text + Environment.NewLine;
                }
                else
                {
                    txtConsole.SelectionStart = txtConsole.TextLength;
                    txtConsole.SelectionLength = 0;
                    txtConsole.SelectionFont = new Font(txtConsole.Font, FontStyle.Regular);
                    //txtConsole.SelectedText = text + Environment.NewLine;
                }

                // Append the text to the RichTextBox
                txtConsole.SelectedText = text;

                SendMessage(txtConsole.Handle, WM_VSCROLL, SB_PAGEBOTTOM, IntPtr.Zero);
            };

            // Invoke if required, otherwise execute directly
            if (this.InvokeRequired)
            {
                this.Invoke(appendAction);
            }
            else
            {
                appendAction();
            }
        }


        private void SaveWindowSettings()
        {
            // Access the value of "txtCard User" text box
            TextBox txtUser = parametersPanel.Controls["txtCard User"] as TextBox;
            string userValue = txtUser?.Text.Trim(); // Get the value, trimming any whitespace

            // Replace with "{{user}}" if empty or null
            if (string.IsNullOrEmpty(userValue))
            {
                userValue = "{{user}}";
            }

            // Get the current background, font colors, and font
            var backgroundColor = txtConsole.BackColor; // Assuming the same color is used for all controls
            var fontColor = txtConsole.ForeColor;       // Assuming the same font color is used for all controls
            var font = txtConsole.Font;

            // Define the window settings to be saved, including the user value, colors, and font
            var windowSettings = new
            {
                Size = this.Size,
                Location = this.Location,
                Maximized = this.WindowState == FormWindowState.Maximized,
                UserValue = userValue, // Add the text box value to the settings
                BackgroundColor = ColorTranslator.ToHtml(backgroundColor), // Save color as HTML color string
                FontColor = ColorTranslator.ToHtml(fontColor),             // Save font color as HTML color string
                FontFamily = font.FontFamily.Name,                        // Save font family
                FontSize = font.Size,                                     // Save font size
                FontStyle = font.Style.ToString()                         // Save font style
            };

            // Define the path for the settings file within the app's folder
            string settingsFilePath = Path.Combine(Application.StartupPath, "aiMultiFool-Settings.json");

            // Serialize the window settings and save them to the file
            File.WriteAllText(settingsFilePath, JsonConvert.SerializeObject(windowSettings, Formatting.Indented));
        }


        private void LoadSettings()
        {
            // Define the path for the settings file within the app's folder
            string settingsFilePath = Path.Combine(Application.StartupPath, "aiMultiFool-Settings.json");
            Color defaultBackgroundColor = Color.FromArgb(255, 255, 255);



            if (File.Exists(settingsFilePath))
            {
                // Read the settings file
                string settingsJson = File.ReadAllText(settingsFilePath);
                dynamic settings = JsonConvert.DeserializeObject<dynamic>(settingsJson);

                if (settings != null)
                {
                    // Load UserValue
                    string userValue = settings?.UserValue;
                    TextBox txtUser = parametersPanel.Controls["txtCard User"] as TextBox;
                    if (txtUser != null)
                    {
                        txtUser.Text = userValue ?? string.Empty; // If UserValue is null, set to empty
                    }

                    // Load Size
                    if (settings.Size != null)
                    {
                        string sizeString = settings.Size.ToString();
                        var sizeParts = sizeString.Split(',');
                        if (sizeParts.Length == 2 &&
                            int.TryParse(sizeParts[0].Trim(), out int width) &&
                            int.TryParse(sizeParts[1].Trim(), out int height))
                        {
                            this.Size = new Size(width, height);
                        }
                    }

                    // Load Location
                    if (settings.Location != null)
                    {
                        string locationString = settings.Location.ToString();
                        var locationParts = locationString.Split(',');
                        if (locationParts.Length == 2 &&
                            int.TryParse(locationParts[0].Trim(), out int x) &&
                            int.TryParse(locationParts[1].Trim(), out int y))
                        {
                            this.StartPosition = FormStartPosition.Manual; // Ensure manual location
                            this.Location = new Point(x, y);
                        }
                    }

                    // Load WindowState
                    if (settings.Maximized != null && settings.Maximized == true)
                    {
                        this.WindowState = FormWindowState.Maximized;
                    }

                    // Load BackgroundColor
                    if (settings.BackgroundColor != null)
                    {
                        var backgroundColor = ColorTranslator.FromHtml((string)settings.BackgroundColor);
                        txtConsole.BackColor = backgroundColor;
                        txtUserInput.BackColor = backgroundColor;
                        tpsLabel.BackColor = backgroundColor;

                        InitializeGraphPictureBox();

                        foreach (Control control in parametersPanel.Controls)
                        {
                            if (control is TextBox textBox)
                            {
                                textBox.BackColor = backgroundColor;
                            }
                        }
                    }

                    // Load FontColor
                    if (settings.FontColor != null)
                    {
                        var fontColor = ColorTranslator.FromHtml((string)settings.FontColor);
                        txtConsole.ForeColor = fontColor;
                        txtUserInput.ForeColor = fontColor;
                        tpsLabel.ForeColor = fontColor;

                        foreach (Control control in parametersPanel.Controls)
                        {
                            if (control is Label label)
                            {
                                //label.ForeColor = fontColor;
                            }
                            else if (control is TextBox textBox)
                            {
                                textBox.ForeColor = fontColor;
                            }
                        }
                    }

                    // Load Font
                    if (settings.FontFamily != null && settings.FontSize != null && settings.FontStyle != null)
                    {
                        try
                        {
                            string fontFamily = settings.FontFamily;
                            float fontSize = (float)settings.FontSize;
                            FontStyle fontStyle = (FontStyle)Enum.Parse(typeof(FontStyle), (string)settings.FontStyle);

                            Font newFont = new Font(fontFamily, fontSize, fontStyle);

                            txtConsole.Font = newFont;
                            txtUserInput.Font = newFont;
                        }
                        catch (Exception ex)
                        {
                            // Handle invalid font settings gracefully
                            Console.WriteLine($"Error loading font: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                // Settings file not found: Apply default colors and settings
                txtConsole.BackColor = defaultBackgroundColor;
                txtUserInput.BackColor = defaultBackgroundColor;
                tpsLabel.BackColor = defaultBackgroundColor;

                foreach (Control control in parametersPanel.Controls)
                {
                    if (control is TextBox textBox)
                    {
                        textBox.BackColor = defaultBackgroundColor;
                    }
                }

                // Show HelpForm if file doesn't exist
                HelpForm helpForm = new HelpForm();
                helpForm.StartPosition = FormStartPosition.Manual; // Set position manually
                helpForm.Location = new Point(
                    this.Location.X + (this.Width - helpForm.Width) / 2,
                    this.Location.Y + (this.Height - helpForm.Height) / 2
                );
                helpForm.Show();
                helpForm.TopMost = true;
            }
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            txtConsole.TextChanged += txtConsole_TextChanged;

        }

        private void chatGPTAICharacterCardGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // Open the URL
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://chatgpt.com/g/g-k2XkHmLPL-ai-character-card-generator-sillytavern",
                UseShellExecute = true
            });
        }

        private void loadChat(object sender, EventArgs e)
        {
            // Ensure the 'chat-history' folder exists in the working directory
            string chatHistoryFolder = Path.Combine(Environment.CurrentDirectory, "chats-json");
            if (!Directory.Exists(chatHistoryFolder))
            {
                Directory.CreateDirectory(chatHistoryFolder);
            }

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                openFileDialog.Title = "Load Chat Session";

                // Set the initial directory to the 'chat-history' folder
                openFileDialog.InitialDirectory = chatHistoryFolder;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(openFileDialog.FileName);
                        var messages = JsonConvert.DeserializeObject<List<ChatHistory.Message>>(json);

                        if (messages != null)
                        {
                            // Clear the existing chat history by recreating a new list internally
                            // Call the clear context method
                            clearContext();
                            session.History.Messages.Clear();

                            StartNewChat();

                            foreach (var message in messages)
                            {
                                session.History.AddMessage(message.AuthorRole, message.Content);
                            }

                            UpdatePictureBoxImage(pictureBox, null); // Use null or empty string as a signal to reset

                            // Update the console to reflect loaded history
                            txtConsole.Clear();
                            foreach (var message in session.History.Messages)
                            {
                                string content = message.Content;

                                // Check for JSON or starting marker `{`
                                int jsonStartIndex = content.IndexOf('{');
                                if (jsonStartIndex != -1)
                                {
                                    // Skip the entire message if JSON marker is found
                                    continue;
                                }

                                // Filter "Assistant:" and "User:" markers and append remaining content
                                string filteredContent = content.Replace("Assistant:", "").Replace("User:", "").Trim();
                                AppendText($"\n{filteredContent}");
                            }

                            loadedfromCard = false;

                            //MessageBox.Show("Chat Session loaded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            CustomMessageBox.Show("Failed to load chat session: File is empty or invalid, please close app and delete it.", "Error", this, isOkOnly: true);
                        }
                    }
                    catch (Exception)
                    {
                        CustomMessageBox.Show($"Failed to load chat session: File is empty or invalid, please close app and delete it.", "Error", this, isOkOnly: true);
                    }
                }
            }
        }



        private void windowColourToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    // Change background color of txtConsole and txtUserInput
                    txtConsole.BackColor = colorDialog.Color;
                    txtUserInput.BackColor = colorDialog.Color;
                    tpsLabel.BackColor = colorDialog.Color;

                    if (graphPictureBox.Image is Bitmap bmp)
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(colorDialog.Color); // Clears the Bitmap with tpsLabel's BackColor
                        }

                        graphPictureBox.Invalidate(); // Force the PictureBox to refresh
                    }

                    // Change background color of dynamically added text boxes in parametersPanel
                    foreach (Control control in parametersPanel.Controls)
                    {
                        if (control is TextBox textBox)
                        {
                            textBox.BackColor = colorDialog.Color;
                        }
                    }
                    SaveWindowSettings();
                }
            }
        }

        private void fontColourToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    // Change font color of txtConsole and txtUserInput
                    txtConsole.ForeColor = colorDialog.Color;
                    txtUserInput.ForeColor = colorDialog.Color;
                    tpsLabel.ForeColor = colorDialog.Color;

                    // Change font color of dynamically added labels and text boxes in parametersPanel
                    foreach (Control control in parametersPanel.Controls)
                    {
                        if (control is Label label)
                        {
                            // label.ForeColor = colorDialog.Color;
                        }
                        else if (control is TextBox textBox)
                        {
                            textBox.ForeColor = colorDialog.Color;
                        }
                    }
                    SaveWindowSettings();
                }
            }
        }

        private void clearContext(bool keepCardImageOpen = false)
        {
            // Check if any open forms have the name "CardImage"
            //var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");

            // If the form is found and we're not instructed to keep it open, close it
            //if (openForm != null && !keepCardImageOpen)
            //{
            //    openForm.Close();
            //}

            try
            {
                // Save the current context into the buffer
                if (session?.History?.Messages != null)
                {
                    contextBuffer = session.History.Messages.ToList();
                }

                // Dispose of the existing context to release memory and reset state
                if (context != null)
                {
                    context.Dispose();
                }

                // Reinitialize the context
                var parameters = new ModelParams(selectedModelPath)
                {
                    GpuLayerCount = int.Parse(GetParameterValue("GpuLayerCount")),
                    ContextSize = uint.Parse(GetParameterValue("ContextSize"))
                };

                context = model.CreateContext(parameters);

                // Reinitialize the executor and session with the new context
                var executor = new InteractiveExecutor(context);
                session = new ChatSession(executor, new ChatHistory());

                // Apply necessary transformations to the new session
                session.WithHistoryTransform(new PromptTemplateTransformer(model, withAssistant: true));
                session.WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
                    new[] { model.Tokens.EndOfTurnToken ?? "User:", "�", "im_end", "<|" }, 10));



            }
            catch (Exception ex)
            {
                AppendText($"Error clearing context: {ex.Message}\r\n");
            }
        }

        private void ModifyContextBuffer(int messagesToRemove)
        {
            int minimumMessages = 2; // Minimum messages to preserve: first user input + first AI reply
            if (contextBuffer.Count > minimumMessages)
            {
                int removableMessages = Math.Min(messagesToRemove, contextBuffer.Count - minimumMessages);
                contextBuffer.RemoveRange(contextBuffer.Count - removableMessages, removableMessages);
            }
        }


        private void RestoreContext()
        {
            try
            {
                if (contextBuffer != null && contextBuffer.Count > 0)
                {
                    session.History.Messages.Clear(); // Clear the existing session history

                    foreach (var message in contextBuffer)
                    {
                        // Add content directly to the session history
                        session.History.AddMessage(message.AuthorRole, message.Content);
                    }

                    AppendText("AI context has been restored from the buffer.\r\n");

                    // Reload all content into the console
                    txtConsole.Clear();

                    foreach (var message in session.History.Messages)
                    {
                        string content = message.Content;

                        // Check for the presence of JSON or starting marker `{`
                        int jsonStartIndex = content.IndexOf('{');
                        if (jsonStartIndex != -1)
                        {
                            // Skip the entire message that contains JSON and anything preceding it
                            continue;
                        }

                        // Append the remaining content to txtConsole
                        AppendText($"\n{content}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error restoring context: {ex.Message}\r\n");
            }
        }



        private void saveChatSessionToolStripMenuItem1_Click(object sender, EventArgs e)
        {

            if (session?.History == null)
            {
                CustomMessageBox.Show("No Chat Session available to save.", "Error", this, isOkOnly: true);

                return;
            }

            // Show a confirmation dialog
            var confirmResult = CustomMessageBox.Show("Are you sure you want to save this chat session? \nComplete privacy is lost as the chat session will exist as a file.", "Confirm Chat Session Save", this, isOkOnly: false);


            if (confirmResult != DialogResult.Yes)
            {
                return; // User chose not to save
            }

            // Ensure the 'chat-history' folder exists in the working directory
            string chatHistoryFolder = Path.Combine(Environment.CurrentDirectory, "chats-json");
            if (!Directory.Exists(chatHistoryFolder))
            {
                Directory.CreateDirectory(chatHistoryFolder);
            }

            // Open a save file dialog
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save Chat Session";
                saveFileDialog.DefaultExt = "json";
                saveFileDialog.FileName = "my chat session.json";

                // Set the initial directory to the 'chat-history' folder
                saveFileDialog.InitialDirectory = chatHistoryFolder;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Serialize chat history to JSON
                        string json = JsonConvert.SerializeObject(session.History.Messages, Formatting.Indented);
                        File.WriteAllText(saveFileDialog.FileName, json);
                        CustomMessageBox.Show("Chat Session saved successfully.", "Success", this, isOkOnly: true);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Failed to save Chat Session: {ex.Message}", "Error", this, isOkOnly: true);
                    }
                }
            }
        }

        private void loadChatSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check for existing chat sessions in the 'chat-sessions' folder
            string chatSessionsPath = Path.Combine(Directory.GetCurrentDirectory(), "chats-json");

            loadedfromCard = false;
            btnRestartCard.Enabled = false;

            loadChat(this, EventArgs.Empty);

            var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");

            // If the form is found and we're not instructed to keep it open, close it
            if (openForm != null)
            {
                openForm.Close();
            }


        }

        private async void loadSillyTavernV2PNGCharacterCardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Define the path to the "cards" folder
            string cardsFolderPath = Path.Combine(Application.StartupPath, "cards-png");




            // Create and configure the file dialog
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*";
                openFileDialog.Title = "Load PNG Character Card";

                // Set initial directory to "cards" folder if it exists
                if (Directory.Exists(cardsFolderPath))
                {
                    openFileDialog.InitialDirectory = cardsFolderPath;
                }

                // Show the dialog and check if the user selected a file
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {

                    var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");
                    // If the form is found and we're not instructed to keep it open, close it
                    if (openForm != null)
                    {
                        openForm.Close();
                    }



                    // Get the selected file path
                    string filePath = openFileDialog.FileName;

                    try
                    {
                        // Call the clear context method

                        txtConsole.Clear();
                        txtUserInput.Clear();
                        btnRestartCard.Enabled = false;

                        AppendText("AI is processing Character Card please wait..\r\n");

                        clearContext();

                        StartNewChat();

                        AppendText("\r\n");

                        // Load the image
                        Image image = Image.FromFile(filePath);


                        // Extract the filename without extension
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                        // Display the image in the CardImage form
                        CardImage cardImageForm = new CardImage
                        {
                            PictureBox = { Image = image }
                        };
                        // Set the form's title to "Now Playing - " followed by the file name without extension
                        cardImageForm.Text = $"Now Playing - {fileNameWithoutExtension}";




                        // Calculate and set form size based on image aspect ratio
                        int initialFormWidth = 600;
                        float aspectRatio = (float)image.Height / image.Width;
                        int calculatedHeight = (int)(initialFormWidth * aspectRatio);
                        int borderWidth = cardImageForm.Width - cardImageForm.ClientSize.Width;
                        int titleBarHeight = cardImageForm.Height - cardImageForm.ClientSize.Height;

                        cardImageForm.Width = initialFormWidth + borderWidth;
                        cardImageForm.Height = calculatedHeight + titleBarHeight;

                        // Center the CardImage form on the main application's window
                        Rectangle mainFormBounds = this.Bounds; // Get the bounds of the main form
                        int centerX = mainFormBounds.Left + (mainFormBounds.Width - cardImageForm.Width) / 2;
                        int centerY = mainFormBounds.Top + (mainFormBounds.Height - cardImageForm.Height) / 2;
                        cardImageForm.StartPosition = FormStartPosition.Manual; // Set to manual positioning
                        cardImageForm.Location = new Point(centerX, centerY);

                        // Show the form non-modally

                        cardImageForm.Show();

                        UpdatePictureBoxImage(pictureBox, filePath);

                        // Process the image metadata
                        using (var magickImage = new MagickImage(filePath))
                        {
                            string metadata = magickImage.GetAttribute("chara");
                            if (metadata == null)
                            {
                                CustomMessageBox.Show("No JSON metadata found in the image.", "Error", this, isOkOnly: true);
                                return;
                            }

                            // Decode Base64 and parse JSON
                            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(metadata));
                            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(decodedJson);

                            // Extract and process "talkativeness" and "depth_prompt"
                            TextBox txtTalkativeness = parametersPanel.Controls["txtAI Talkativeness"] as TextBox;
                            TextBox txtDepthPrompt = parametersPanel.Controls["txtAI Depth"] as TextBox;

                            string talkativenessStr = jsonObject?.talkativeness?.ToString() ?? "0.5";
                            string depthPromptStr = jsonObject?.data?.extensions?.depth_prompt?.depth?.ToString() ?? "5";

                            // Convert the talkativeness to double
                            if (!double.TryParse(talkativenessStr, out double talkativeness))
                            {
                                talkativeness = 0.5; // Fallback in case parsing fails
                            }

                            // Update the txtTalkativeness textbox
                            if (txtTalkativeness != null)
                            {
                                txtTalkativeness.Text = talkativeness.ToString("0.0"); // Format to one decimal place
                                talk = talkativeness.ToString("0.0");
                            }

                            // Update the txtDepthPrompt textbox
                            if (txtDepthPrompt != null)
                            {
                                txtDepthPrompt.Text = depthPromptStr; // Display the depth value
                                depth = depthPromptStr;
                            }

                            // Strip formatting and replace placeholders
                            jsonContent = StripFormatting(decodedJson);

                            TextBox txtUser = parametersPanel.Controls["txtCard User"] as TextBox;
                            string userName = txtUser?.Text.Trim() ?? "";

                            jsonContent = jsonContent.Replace("<START>\n", string.Empty)
                                                     .Replace("<START>\\n", string.Empty)
                                                     .Replace("{{user}}", userName);

                            // Construct the updated JSON content with additional descriptive context
                            jsonContent = $@"talkativeness is the length of your replies, it is scaled between 0.1 and 1.0, with 1.0 being the most verbose. Set your talkativeness to {talkativeness.ToString("0.0")}. depth is used to adjust response detail. It provides guidance on tone or complexity, where 1 is concise and 5 is highly detailed and immersive. Set your depth to {depthPromptStr}. Start a roleplay scenario based on the following data. Never type {{char}} or {{user}}. {jsonContent}";
                            jsonRestartCard = jsonContent;
                            loadedfromCard = true;

                            ReloadInferenceParams();

                            // Handle the decoded JSON content
                            await HandleInput(jsonContent, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Error processing file: {ex.Message}", "Error", this, isOkOnly: true);
                    }
                }
            }
        }

        private async void LoadSillyTavernV2JSONCharacterCardToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // Define the path to the "cards" folder
            string cardsFolderPath = Path.Combine(Application.StartupPath, "cards-json");

            // Create and configure the file dialog
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Update filter to include JSON files
                openFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                openFileDialog.Title = "Load JSON Character Card";

                // Set initial directory to "cards" folder if it exists
                if (Directory.Exists(cardsFolderPath))
                {
                    openFileDialog.InitialDirectory = cardsFolderPath;
                }

                // Show the dialog and check if the user selected a file
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {

                    UpdatePictureBoxImage(pictureBox, null); // Use null or empty string as a signal to reset


                    // Get the selected file path
                    string filePath = openFileDialog.FileName;

                    try
                    {
                        txtConsole.Clear();
                        txtUserInput.Clear();
                        btnRestartCard.Enabled = false;
                        AppendText("AI is processing Character Card please wait..\r\n");

                        clearContext();

                        StartNewChat();

                        AppendText("\r\n");


                        var openForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Name == "CardImage");

                        // If the form is found and we're not instructed to keep it open, close it
                        if (openForm != null)
                        {
                            openForm.Close();
                        }


                        // Read JSON file content
                        jsonContent = File.ReadAllText(filePath);

                        // Read the character and user names directly from the text boxes
                        string userName = "";

                        // Access the values from the text boxes
                        TextBox txtUser = parametersPanel.Controls["txtCard User"] as TextBox;
                        TextBox txtTalkativeness = parametersPanel.Controls["txtAI Talkativeness"] as TextBox;
                        TextBox txtDepthPrompt = parametersPanel.Controls["txtAI Depth"] as TextBox;

                        if (txtUser != null)
                        {
                            userName = txtUser.Text.Trim(); // Get user name from the textbox
                        }

                        // Parse JSON to extract "talkativeness" and "depth_prompt"
                        var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
                        string talkativenessStr = jsonObject?.talkativeness?.ToString() ?? "0.5"; // Default to "0.5" if missing
                        string depthPromptStr = jsonObject?.data?.extensions?.depth_prompt?.depth?.ToString() ?? "5"; // Default to "5"

                        // Convert the talkativeness to double
                        if (!double.TryParse(talkativenessStr, out double talkativeness))
                        {
                            talkativeness = 0.5; // Fallback in case parsing fails
                        }

                        // Update the txtTalkativeness textbox
                        if (txtTalkativeness != null)
                        {
                            txtTalkativeness.Text = talkativeness.ToString("0.0"); // Format to one decimal place
                            talk = talkativeness.ToString("0.0");
                        }

                        // Update the txtDepthPrompt textbox
                        if (txtDepthPrompt != null)
                        {
                            txtDepthPrompt.Text = depthPromptStr; // Display the depth value
                            depth = depthPromptStr;
                        }

                        // Clean up JSON content
                        jsonContent = jsonContent.Replace("<START>\n", string.Empty);
                        jsonContent = jsonContent.Replace("<START>\\n", string.Empty);
                        jsonContent = jsonContent.Replace("{{user}}", userName);

                        // Construct the updated JSON content with additional descriptive context
                        jsonContent = $@"talkativeness is the length of your replies, it is scaled between 0.1 and 1.0, with 1.0 being the most verbose. Set your talkativeness to {talkativeness.ToString("0.0")}. depth is used to adjust response detail. It provides guidance on tone or complexity, where 1 is concise and 5 is highly detailed and immersive. Set your depth to {depthPromptStr}. Start a roleplay scenario based on the following data. Never type {{char}} or {{user}}. {jsonContent}";

                        jsonRestartCard = jsonContent;

                        // Load the corresponding image if it exists
                        string imagePath = Path.ChangeExtension(filePath, ".jpg");
                        if (File.Exists(imagePath))
                        {
                            // Load the image from the file path
                            Image image = Image.FromFile(imagePath);

                            // Display the image in the CardImage form
                            CardImage cardImageForm = new CardImage
                            {
                                // Assign the image to the PictureBox in the CardImage form
                                PictureBox = { Image = image }
                            };

                            // Define the initial width of the form
                            int initialFormWidth = 300;

                            // Calculate the aspect ratio of the image
                            float aspectRatio = (float)image.Height / image.Width;

                            // Calculate the height based on the aspect ratio and initial width
                            int calculatedHeight = (int)(initialFormWidth * aspectRatio);

                            // Calculate the border width and title bar height
                            int borderWidth = cardImageForm.Width - cardImageForm.ClientSize.Width;
                            int titleBarHeight = cardImageForm.Height - cardImageForm.ClientSize.Height;

                            // Set the initial form size
                            cardImageForm.Width = initialFormWidth + borderWidth;
                            cardImageForm.Height = calculatedHeight + titleBarHeight;

                            // Center the CardImage form on the main application's window
                            Rectangle mainFormBounds = this.Bounds; // Get the bounds of the main form
                            int centerX = mainFormBounds.Left + (mainFormBounds.Width - cardImageForm.Width) / 2;
                            int centerY = mainFormBounds.Top + (mainFormBounds.Height - cardImageForm.Height) / 2;
                            cardImageForm.StartPosition = FormStartPosition.Manual; // Set to manual positioning
                            cardImageForm.Location = new Point(centerX, centerY);

                            // Show the form non-modally
                            cardImageForm.Show();

                            // Extract the filename without extension
                            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                            // Set the CardImage form's title bar text
                            cardImageForm.Text = fileNameWithoutExtension;
                        }

                        loadedfromCard = true;

                        ReloadInferenceParams();

                        // Process the JSON content
                        await HandleInput(jsonContent, true);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Error processing file: {ex.Message}", "Error", this, isOkOnly: true);
                    }
                }
            }
        }

        private async void loadNewModelToolStripMenuItem_Click(object sender, EventArgs e)
        {


            TextBox txtTemperature = parametersPanel.Controls["txtTemperature"] as TextBox;
            txtTemperature.Text = "0.7";
            TextBox txtTopP = parametersPanel.Controls["txtTopP"] as TextBox;
            txtTopP.Text = "0.95";
            TextBox txtMinP = parametersPanel.Controls["txtMinP"] as TextBox;
            txtMinP.Text = "0.05";
            TextBox txtTopK = parametersPanel.Controls["txtTopK"] as TextBox;
            txtTopK.Text = "40";
            TextBox txtMaxTokens = parametersPanel.Controls["txtMaxTokens"] as TextBox;
            txtMaxTokens.Text = "-1";
            TextBox txtRepeatPenalty = parametersPanel.Controls["txtRepeatPenalty"] as TextBox;
            txtRepeatPenalty.Text = "1.1";
            ReloadInferenceParams();

            resetSliders();
            disableCardControls();

            contextSize = 0;

            UpdatePictureBoxImage(pictureBox, null); // Use null or empty string as a signal to reset

            StartNewChat();

            // Prompt the user to select a new model
            AppendText("Preparing to load a new model...\r\n");

            // Reset the current session state if necessary
            cancellationTokenSource?.Cancel();

            txtUserInput.Enabled = false;
            clearContext(keepCardImageOpen: false);
            txtConsole.Clear();
            btnModify.Enabled = false;

            TextBox txtGPU = parametersPanel.Controls["txtGpuLayerCount"] as TextBox;
            TextBox txtContext = parametersPanel.Controls["txtContextSize"] as TextBox;
            txtGPU.Enabled = true;
            txtContext.Enabled = true;



            // Call the method to allow the user to select a model
            await PromptUserToSelectModel();
        }


        private void menuEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Check if the form is already open
            if (menuEditor != null && !menuEditor.IsDisposed)
            {
                // If the form is minimized, restore it
                if (menuEditor.WindowState == FormWindowState.Minimized)
                {
                    menuEditor.WindowState = FormWindowState.Normal;
                }

                // Bring the form to the front
                menuEditor.BringToFront();
                menuEditor.Focus();
            }
            else
            {
                // Create a new instance if it is not already open
                menuEditor = new MenuEditorForm
                {
                    StartPosition = FormStartPosition.Manual
                };

                // Center the form manually
                menuEditor.Location = new Point(
                    this.Location.X + (this.Width - menuEditor.Width) / 2,
                    this.Location.Y + (this.Height - menuEditor.Height) / 2
                );

                menuEditor.OnMenusSaved += ReloadMenus; // Subscribe to the event
                menuEditor.FormClosed += (s, args) => menuEditor = null; // Clear the reference when the form is closed
                menuEditor.Show();
            }
        }


        private void quickstartGuideToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            HelpForm helpForm = new HelpForm();
            helpForm.StartPosition = FormStartPosition.Manual; // Set position manually
            helpForm.Location = new Point(this.Location.X + (this.Width - helpForm.Width) / 2,
                                          this.Location.Y + (this.Height - helpForm.Height) / 2);
            helpForm.Show();
        }

        private void huggingFaceToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://huggingface.co/",
                UseShellExecute = true
            });
        }

        private void aicharactercardscomToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://aicharactercards.com/",
                UseShellExecute = true
            });
        }

        private void characterhuborgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.characterhub.org/",
                UseShellExecute = true
            });
        }

        private void aiMultiFoolcomToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://aimultifool.com/",
                UseShellExecute = true
            });
        }

        private async void downloadRocinante12Bv11Q4KMggufToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string modelsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf");

            txtConsole.Clear();
            clearContext(keepCardImageOpen: false);
            menuStrip1.Enabled = false;
            DisableAllButtons(this);

            AppendText("Downloading 12b uncensored roleplay model.. please be patient.\r\n");
            AppendText("Rocinante-12B-v1.1-Q4_K_M.gguf\r\n");
            AppendText("\r\n");
            // Download the first model
            string modelUrl1 = "https://huggingface.co/TheDrummer/Rocinante-12B-v1.1-GGUF/resolve/main/Rocinante-12B-v1.1-Q4_K_M.gguf?download=true";
            string modelPath1 = Path.Combine(modelsFolder, "Card Roleplay Uncensored Rocinante-12B-v1.1-Q4_K_M.gguf");

            await DownloadModelAsync(modelUrl1, modelPath1);

            downloadRocinante12Bv11Q4KMggufToolStripMenuItem.Visible = false;

            // Reset the current session state if necessary
            cancellationTokenSource?.Cancel();
            txtConsole.Clear();

            TextBox txtGPU = parametersPanel.Controls["txtGpuLayerCount"] as TextBox;
            TextBox txtContext = parametersPanel.Controls["txtContextSize"] as TextBox;
            txtGPU.Enabled = true;
            txtContext.Enabled = true;



            // Call the method to allow the user to select a model
            await PromptUserToSelectModel();

        }

        private async void downloadLlama323BInstructuncensoredQ4KMggufToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string modelsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models-gguf");

            txtConsole.Clear();
            clearContext(keepCardImageOpen: false);
            menuStrip1.Enabled = false;
            DisableAllButtons(this);

            AppendText("Downloading 3b censored general chat model.. please be patient\r\n");
            AppendText("Llama-3.2-3B-Instruct-Q4_K_M.gguf\r\n");
            AppendText("\r\n");
            // Download the second model
            string modelUrl3 = "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf?download=true";
            string modelPath3 = Path.Combine(modelsFolder, "General Chat Censored Llama-3.2-3B-Instruct-Q4_K_M.gguf");

            await DownloadModelAsync(modelUrl3, modelPath3);

            downloadLlama323BInstructuncensoredQ4KMggufToolStripMenuItem.Visible = false;

            // Reset the current session state if necessary
            cancellationTokenSource?.Cancel();
            txtConsole.Clear();

            TextBox txtGPU = parametersPanel.Controls["txtGpuLayerCount"] as TextBox;
            TextBox txtContext = parametersPanel.Controls["txtContextSize"] as TextBox;
            txtGPU.Enabled = true;
            txtContext.Enabled = true;



            // Call the method to allow the user to select a model
            await PromptUserToSelectModel();


        }

        private void DisableAllButtons(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Button)
                {
                    control.Enabled = false;
                }
                else if (control.HasChildren)
                {
                    // Recursively disable buttons in child controls (e.g., panels, group boxes)
                    DisableAllButtons(control);
                }
            }
        }

        private void createSillyTavernPNGCardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Step 1: Open file dialog to select the PNG image
                OpenFileDialog openPngDialog = new OpenFileDialog
                {
                    Filter = "PNG Files (*.png)|*.png",
                    Title = "Select a PNG Image for your Card"
                };

                if (openPngDialog.ShowDialog() != DialogResult.OK)
                {
                    //MessageBox.Show("No PNG file selected.");
                    return;
                }

                string pngFilePath = openPngDialog.FileName;

                // Step 2: Open file dialog to select the JSON file
                OpenFileDialog openJsonDialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "Select a JSON File in SillyTavern Format"
                };

                if (openJsonDialog.ShowDialog() != DialogResult.OK)
                {
                    //MessageBox.Show("No JSON file selected.");
                    return;
                }

                string jsonFilePath = openJsonDialog.FileName;

                // Step 3: Validate and combine the PNG and JSON
                string base64CharacterData;
                try
                {
                    // Read and validate JSON content
                    jsonContent = File.ReadAllText(jsonFilePath);
                    var jsonObject = JsonConvert.DeserializeObject(jsonContent); // Ensure it's valid JSON
                    base64CharacterData = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)); // Encode in Base64
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Invalid JSON file: {ex.Message}", "Error", this, isOkOnly: true);

                    return;
                }

                // Step 4: Prompt user to save the output PNG
                SaveFileDialog savePngDialog = new SaveFileDialog
                {
                    Filter = "PNG Files (*.png)|*.png",
                    Title = "Save the Combined PNG File",
                    FileName = "card_with_metadata.png"
                };

                if (savePngDialog.ShowDialog() != DialogResult.OK)
                {
                    //MessageBox.Show("Save operation cancelled.");
                    return;
                }

                string outputPngPath = savePngDialog.FileName;

                // Step 5: Combine PNG and JSON using Magick.NET
                try
                {
                    using (var image = new MagickImage(pngFilePath))
                    {
                        image.SetAttribute("chara", base64CharacterData); // Embed the JSON data
                        image.Write(outputPngPath); // Save the image
                    }

                    CustomMessageBox.Show($"SillyTavern PNG card saved successfully at:\n{outputPngPath}", "Success", this, isOkOnly: true);

                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"An error occurred while embedding metadata: {ex.Message}", "Error", this, isOkOnly: true);

                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", this, isOkOnly: true);

            }
        }

        private void editSillyTavernPNGCardMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Step 1: Configure the OpenFileDialog to select a PNG file
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PNG Files (*.png)|*.png",
                Title = "Select a PNG Image with Metadata"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return; // User canceled
            }

            string pngFilePath = openFileDialog.FileName;

            try
            {
                // Step 2: Load the JSON metadata from the PNG file
                string metadata;
                using (var image = new MagickImage(pngFilePath))
                {
                    metadata = image.GetAttribute("chara");

                    if (metadata == null)
                    {
                        CustomMessageBox.Show("No JSON metadata found in the selected PNG.", "Error", this, isOkOnly: true);
                        return;
                    }
                }

                // Step 3: Decode Base64 and format the JSON
                string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(metadata));
                string formattedJson;
                try
                {
                    var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(decodedJson);
                    formattedJson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObject, Newtonsoft.Json.Formatting.Indented);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error formatting JSON: {ex.Message}", "Error", this, isOkOnly: true);
                    return;
                }

                EditPNG editForm = new EditPNG(formattedJson)
                {
                    Text = Path.GetFileName(pngFilePath) // Set the form title to the filename
                };

                // Calculate the center position based on the parent form's location
                int parentCenterX = this.Location.X + (this.Width - editForm.Width) / 2;
                int parentCenterY = this.Location.Y + (this.Height - editForm.Height) / 2;

                // Adjust for multi-screen boundaries
                Rectangle workingArea = Screen.FromControl(this).WorkingArea;
                parentCenterX = Math.Max(workingArea.Left, Math.Min(parentCenterX, workingArea.Right - editForm.Width));
                parentCenterY = Math.Max(workingArea.Top, Math.Min(parentCenterY, workingArea.Bottom - editForm.Height));

                // Set the location of the form
                editForm.StartPosition = FormStartPosition.Manual; // Manual positioning
                editForm.Location = new Point(parentCenterX, parentCenterY);

                // Pass the PNG file path to the EditPNG form
                editForm.Tag = pngFilePath; // Store the file path in the form's Tag property

                // Display the form as a normal, non-modal window
                editForm.Show(); // Open the form
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", this, isOkOnly: true);
            }
        }

        private void fontTypeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                // Set initial font to the current font of txtConsole
                fontDialog.Font = txtConsole.Font;

                // Show the dialog and check if the user clicked OK
                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    // Apply the selected font to both txtConsole and txtUserInput
                    txtConsole.Font = fontDialog.Font;
                    txtUserInput.Font = fontDialog.Font;
                    SaveWindowSettings();
                }
            }
        }
    }
}






