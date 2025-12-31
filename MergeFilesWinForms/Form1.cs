using System.Text;

namespace MergeFilesWinForms
{
    public partial class Form1 : Form
    {
        // UI
        private readonly ListBox lstFiles = new ListBox();
        private readonly Button btnMerge = new Button();
        private readonly Button btnAddFolder = new Button();
        private readonly Button btnRemoveSelected = new Button();
        private readonly Button btnClear = new Button();
        private readonly Button btnEditAllow = new Button();
        private readonly Button btnEditIgnore = new Button();
        private readonly Button btnEditInclude = new Button();
        private readonly Label lblCount = new Label();
        private readonly Label lblPrefix = new Label();
        private readonly TextBox txtPrefix = new TextBox();
        // Durum
        private readonly List<string> _files = new List<string>();

        // Config dosyaları
        private readonly string allowFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allow.txt");
        private readonly string ignoreFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ignore.txt");
        private readonly string includeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "include.txt");

        public Form1()
        {
            InitializeComponent();
            EnsureConfigFiles();
            BuildUi();
            HookDnD();
        }

        private void EnsureConfigFiles()
        {
            if (!File.Exists(allowFile))
            {
                File.WriteAllLines(allowFile, new[]
                {
                    ".c",".cpp",".h",".hpp",".cs",".js",".ts",".tsx",".css",".xaml",".xml",".json",
                    ".html",".md",".ini",".cfg",".py",".sql",".shader"
                });
            }

            if (!File.Exists(ignoreFile))
            {
                File.WriteAllLines(ignoreFile, new[]
                {
                    "bin","obj","node_modules","wwwroot/vendor"
                });
            }

            if (!File.Exists(includeFile))
            {
                File.WriteAllLines(includeFile, new[]
                {
                    "# Ignore edilen klasörlerdeki spesifik dosyaları dahil etmek için",
                    "# Göreceli yol kullanın (örnek: build/zephyr/zephyr.dts)",
                    ""
                });
            }
        }

        private void BuildUi()
        {
            Text = "Merge Files (Drag Drop)";
            Width = 1000;
            Height = 650;
            StartPosition = FormStartPosition.CenterScreen;

            // Liste
            lstFiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstFiles.HorizontalScrollbar = true;
            lstFiles.IntegralHeight = false;
            lstFiles.Left = 10;
            lstFiles.Top = 10;
            lstFiles.Width = ClientSize.Width - 20;
            lstFiles.Height = ClientSize.Height - 190;
            lstFiles.BorderStyle = BorderStyle.FixedSingle;

            // Üst satır butonları (dosya işlemleri)
            btnAddFolder.Text = "Klasör Ekle";
            btnAddFolder.Width = 100;
            btnAddFolder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAddFolder.Left = 10;
            btnAddFolder.Top = ClientSize.Height - 80;
            btnAddFolder.Click += (s, e) => AddFolder();

            btnRemoveSelected.Text = "Seçileni Kaldır";
            btnRemoveSelected.Width = 110;
            btnRemoveSelected.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnRemoveSelected.Left = btnAddFolder.Right + 10;
            btnRemoveSelected.Top = btnAddFolder.Top;
            btnRemoveSelected.Click += (s, e) => RemoveSelected();

            btnClear.Text = "Temizle";
            btnClear.Width = 80;
            btnClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnClear.Left = btnRemoveSelected.Right + 10;
            btnClear.Top = btnAddFolder.Top;
            btnClear.Click += (s, e) => { _files.Clear(); RefreshList(); };

            // Sayaç ve prefix (üst satırda)
            lblCount.AutoSize = true;
            lblCount.Left = btnClear.Right + 20;
            lblCount.Top = btnAddFolder.Top + 5;
            lblCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            UpdateCount();

            lblPrefix.AutoSize = true;
            lblPrefix.Text = "Dosya adı ön eki:";
            lblPrefix.Left = lblCount.Right + 20;
            lblPrefix.Top = btnAddFolder.Top + 5;
            lblPrefix.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            txtPrefix.Width = 150;
            txtPrefix.Left = lblPrefix.Right + 5;
            txtPrefix.Top = btnAddFolder.Top + 2;
            txtPrefix.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            txtPrefix.Text = "SolionBMS";

            // Alt satır butonları (config dosyaları)
            btnEditAllow.Text = "Allow.txt";
            btnEditAllow.Width = 100;
            btnEditAllow.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEditAllow.Left = 10;
            btnEditAllow.Top = ClientSize.Height - 45;
            btnEditAllow.Click += (s, e) => OpenFile(allowFile);

            btnEditIgnore.Text = "Ignore.txt";
            btnEditIgnore.Width = 100;
            btnEditIgnore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEditIgnore.Left = btnEditAllow.Right + 10;
            btnEditIgnore.Top = btnEditAllow.Top;
            btnEditIgnore.Click += (s, e) => OpenFile(ignoreFile);

            btnEditInclude.Text = "Include.txt";
            btnEditInclude.Width = 100;
            btnEditInclude.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEditInclude.Left = btnEditIgnore.Right + 10;
            btnEditInclude.Top = btnEditAllow.Top;
            btnEditInclude.Click += (s, e) => OpenFile(includeFile);

            // Birleştir butonu (sağ alt)
            btnMerge.Text = "Birleştir";
            btnMerge.Width = 150;
            btnMerge.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnMerge.Left = ClientSize.Width - btnMerge.Width - 10;
            btnMerge.Top = ClientSize.Height - 45;
            btnMerge.Click += (s, e) => SaveCombined();

            Controls.AddRange(new Control[] {
                lstFiles,
                btnMerge,
                btnAddFolder,
                btnRemoveSelected,
                btnClear,
                btnEditAllow,
                btnEditIgnore,
                btnEditInclude,
                lblCount,
                lblPrefix,
                txtPrefix
            });

            // Resize
            Resize += (s, e) =>
            {
                lstFiles.Width = ClientSize.Width - 20;
                lstFiles.Height = ClientSize.Height - 190;
                btnMerge.Left = ClientSize.Width - btnMerge.Width - 10;
                btnMerge.Top = ClientSize.Height - 45;
                btnAddFolder.Top = ClientSize.Height - 80;
                btnRemoveSelected.Top = ClientSize.Height - 80;
                btnClear.Top = ClientSize.Height - 80;
                btnEditAllow.Top = ClientSize.Height - 45;
                btnEditIgnore.Top = ClientSize.Height - 45;
                btnEditInclude.Top = ClientSize.Height - 45;
                lblCount.Top = ClientSize.Height - 75;
                lblPrefix.Top = ClientSize.Height - 75;
                txtPrefix.Top = ClientSize.Height - 78;
            };
        }

        private void HookDnD()
        {
            AllowDrop = true;

            DragEnter += (s, e) =>
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            };

            DragDrop += (s, e) =>
            {
                if (e.Data == null) return;
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddPaths(paths);
            };
        }

        private void AddFolder()
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog(this) == DialogResult.OK)
                AddPaths(new[] { fbd.SelectedPath });
        }

        private void RemoveSelected()
        {
            var toRemove = lstFiles.SelectedItems.Cast<string>().ToList();
            if (toRemove.Count == 0) return;

            foreach (var p in toRemove)
                _files.RemoveAll(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));

            RefreshList();
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            if (paths == null) return;

            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories).OrderBy(c => c))
                        {
                            if (IsAllowed(f) && (IsIncluded(f) || !IsIgnored(f)))
                                AddIfNew(f);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Klasör okunamadı: {p}\n{ex.Message}", "Hata",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if (File.Exists(p) && IsAllowed(p) && (IsIncluded(p) || !IsIgnored(p)))
                {
                    AddIfNew(p);
                }
            }

            RefreshList();
        }

        private HashSet<string> GetAllowedExt()
        {
            try
            {
                return new HashSet<string>(
                    File.ReadAllLines(allowFile)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim())
                        .Where(l => l.StartsWith(".")),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private List<string> GetIgnoredFolders()
        {
            try
            {
                return File.ReadAllLines(ignoreFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private List<string> GetIncludedPaths()
        {
            try
            {
                return File.ReadAllLines(includeFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                    .Select(l => l.Trim())
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private bool IsIncluded(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var normalizedPath = Path.GetFullPath(path)
                                     .Replace('\\', '/')
                                     .ToLowerInvariant();

            foreach (var rule in GetIncludedPaths())
            {
                if (string.IsNullOrWhiteSpace(rule)) continue;

                var normalizedRule = rule.Replace('\\', '/')
                                         .Trim('/')
                                         .ToLowerInvariant();

                // Yolun sonunda bu kural var mı? (suffix match)
                if (normalizedPath.EndsWith("/" + normalizedRule, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool IsAllowed(string path)
            => GetAllowedExt().Contains(Path.GetExtension(path) ?? string.Empty);

        private static readonly char[] _seps = new[] { '\\', '/' };

        private bool IsIgnored(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var segments = Path.GetFullPath(path)
                               .TrimEnd(_seps)
                               .ToLowerInvariant()
                               .Split(_seps, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in GetIgnoredFolders())
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var ruleSegs = raw.Replace('\\', '/')
                                  .Trim('/')
                                  .ToLowerInvariant()
                                  .Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (ruleSegs.Length == 0) continue;

                if (ruleSegs.Length == 1)
                {
                    if (segments.Any(s => s == ruleSegs[0]))
                        return true;
                }
                else
                {
                    if (ContainsSubsequence(segments, ruleSegs))
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsSubsequence(string[] haystack, string[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (!haystack[i + j].Equals(needle[j], StringComparison.OrdinalIgnoreCase))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return true;
            }
            return false;
        }

        private void AddIfNew(string path)
        {
            if (!_files.Contains(path, StringComparer.OrdinalIgnoreCase))
                _files.Add(path);
        }

        private void RefreshList()
        {
            lstFiles.BeginUpdate();
            try
            {
                lstFiles.Items.Clear();
                foreach (var f in _files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                    lstFiles.Items.Add(f);
            }
            finally { lstFiles.EndUpdate(); }

            UpdateCount();
        }

        private void UpdateCount()
            => lblCount.Text = $"Toplam dosya: {lstFiles.Items.Count}";

        private void SaveCombined()
        {
            if (_files.Count == 0)
            {
                MessageBox.Show(this, "Birleştirilecek dosya yok.", "Uyarı",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var prefix = txtPrefix.Text?.Trim();
            if (string.IsNullOrWhiteSpace(prefix))
            {
                MessageBox.Show(this, "Lütfen dosya adı için bir ön ek girin.", "Uyarı",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPrefix.Focus();
                return;
            }
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
                prefix = prefix.Replace(ch, '_');

            var fileName = $"{prefix}{DateTime.Now.ToString("yyMMddhhmm")}";
            using var sfd = new SaveFileDialog
            {
                Title = "Birleştirilmiş dosyayı kaydet",
                Filter = "Metin Dosyası|*.txt|Tümü|*.*",
                FileName = fileName,
                OverwritePrompt = true
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(fs, new UTF8Encoding(false));

                foreach (var file in _files)
                {
                    writer.WriteLine($"===== {Path.GetFileName(file)} =====");
                    writer.WriteLine();

                    string content;
                    try { content = File.ReadAllText(file, DetectEncoding(file)); }
                    catch { content = File.ReadAllText(file, new UTF8Encoding(false)); }

                    writer.WriteLine(content);
                    writer.WriteLine();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Kaydetme hatası: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{sfd.FileName}\"");
            }
            catch { }
        }

        private static Encoding DetectEncoding(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (fs.Length >= 4)
            {
                var bom = new byte[4];
                _ = fs.Read(bom, 0, 4);
                if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true);
                if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
                if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
                if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00) return Encoding.UTF32;
                if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF) return new UTF32Encoding(true, true);
            }
            return new UTF8Encoding(false);
        }

        private void OpenFile(string path)
        {
            try
            {
                System.Diagnostics.Process.Start("notepad.exe", $"\"{path}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Dosya açılamadı: {ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
