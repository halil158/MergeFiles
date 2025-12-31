using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace MergeFilesWPF;

public partial class MainWindow : Window
{
    private readonly List<string> _files = new();
    private static readonly char[] _seps = ['\\', '/'];

    // Config dosyaları
    private readonly string _allowFile;
    private readonly string _ignoreFile;
    private readonly string _includeFile;

    // Windows API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _allowFile = Path.Combine(baseDir, "allow.txt");
        _ignoreFile = Path.Combine(baseDir, "ignore.txt");
        _includeFile = Path.Combine(baseDir, "include.txt");

        EnsureConfigFiles();
        UpdateUI();

        // Dark title bar
        Loaded += (s, e) => ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int value = 1; // Enable dark mode
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
    }

    private void EnsureConfigFiles()
    {
        if (!File.Exists(_allowFile))
        {
            File.WriteAllLines(_allowFile, new[]
            {
                ".c", ".cpp", ".h", ".hpp", ".cs", ".js", ".ts", ".tsx", ".css", ".xaml", ".xml", ".json",
                ".html", ".md", ".ini", ".cfg", ".py", ".sql", ".shader", ".cshtml", ".csproj", ".sln",
                ".slnx", ".ps1", ".gitignore", ".kt", ".txt", ".projbuild", ".overlay", ".conf",
                ".dts", ".dtsi", ".yaml", ".yml", ".cmake", ".indir"
            });
        }

        if (!File.Exists(_ignoreFile))
        {
            File.WriteAllLines(_ignoreFile, new[]
            {
                "bin", "obj", "node_modules", "wwwroot/vendor", "wwwroot/vendors",
                "Lib", "build", "logs", ".claude", ".vs"
            });
        }

        if (!File.Exists(_includeFile))
        {
            File.WriteAllLines(_includeFile, new[]
            {
                "# Ignore edilen klasörlerdeki spesifik dosyaları dahil etmek için",
                "# Göreceli yol kullanın (örnek: build/zephyr/zephyr.dts)",
                ""
            });
        }
    }

    #region Drag & Drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            ShowDragOverlay(true);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        ShowDragOverlay(false);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        ShowDragOverlay(false);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddPaths(paths);
        }
    }

    private void ShowDragOverlay(bool show)
    {
        var animation = new DoubleAnimation
        {
            To = show ? 0.9 : 0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase()
        };
        dragOverlay.BeginAnimation(OpacityProperty, animation);

        if (show)
        {
            dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237));
        }
        else
        {
            dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 95));
        }
    }

    #endregion

    #region Button Handlers

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Klasör Seçin"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths([dialog.FolderName]);
        }
    }

    private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = lstFiles.SelectedItems.Cast<string>().ToList();
        if (selectedItems.Count == 0)
        {
            ShowMessage("Lütfen kaldırılacak dosyaları seçin.", "Uyarı", MessageBoxImage.Warning);
            return;
        }

        foreach (var item in selectedItems)
        {
            _files.Remove(item);
        }

        UpdateUI();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0) return;

        var result = MessageBox.Show(
            "Tüm dosyaları listeden kaldırmak istediğinize emin misiniz?",
            "Onay",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _files.Clear();
            UpdateUI();
        }
    }

    private void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        SaveCombined();
    }

    private void BtnEditAllow_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(_allowFile);
    }

    private void BtnEditIgnore_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(_ignoreFile);
    }

    private void BtnEditInclude_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(_includeFile);
    }

    #endregion

    #region File Operations

    private void AddPaths(IEnumerable<string> paths)
    {
        var addedCount = 0;

        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories).OrderBy(c => c))
                    {
                        if (IsAllowed(f) && (IsIncluded(f) || !IsIgnored(f)))
                        {
                            if (AddIfNew(f)) addedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"Klasör okunamadı: {p}\n{ex.Message}", "Hata", MessageBoxImage.Error);
                }
            }
            else if (File.Exists(p) && IsAllowed(p) && (IsIncluded(p) || !IsIgnored(p)))
            {
                if (AddIfNew(p)) addedCount++;
            }
        }

        UpdateUI();

        if (addedCount > 0)
        {
            // Başarı animasyonu göster
            AnimateSuccess();
        }
    }

    private bool AddIfNew(string path)
    {
        if (!_files.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _files.Add(path);
            return true;
        }
        return false;
    }

    private void SaveCombined()
    {
        if (_files.Count == 0)
        {
            ShowMessage("Birleştirilecek dosya yok.", "Uyarı", MessageBoxImage.Warning);
            return;
        }

        var prefix = txtPrefix.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            ShowMessage("Lütfen dosya adı için bir ön ek girin.", "Uyarı", MessageBoxImage.Warning);
            txtPrefix.Focus();
            return;
        }

        foreach (var ch in Path.GetInvalidFileNameChars())
            prefix = prefix.Replace(ch, '_');

        var fileName = $"{prefix}{DateTime.Now:yyMMddHHmm}";

        var dialog = new SaveFileDialog
        {
            Title = "Birleştirilmiş dosyayı kaydet",
            Filter = "Metin Dosyası|*.txt|Tümü|*.*",
            FileName = fileName,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var fs = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.Read);
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

            // Dosyayı explorer'da göster
            Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
        }
        catch (Exception ex)
        {
            ShowMessage($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxImage.Error);
        }
    }

    private void OpenFile(string path)
    {
        try
        {
            Process.Start("notepad.exe", $"\"{path}\"");
        }
        catch (Exception ex)
        {
            ShowMessage($"Dosya açılamadı: {ex.Message}", "Hata", MessageBoxImage.Error);
        }
    }

    #endregion

    #region Filtering

    private HashSet<string> GetAllowedExt()
    {
        try
        {
            return new HashSet<string>(
                File.ReadAllLines(_allowFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith('.')),
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
            return File.ReadAllLines(_ignoreFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private List<string> GetIncludedPaths()
    {
        try
        {
            return File.ReadAllLines(_includeFile)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
                .Select(l => l.Trim())
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private bool IsAllowed(string path)
        => GetAllowedExt().Contains(Path.GetExtension(path) ?? string.Empty);

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

            if (normalizedPath.EndsWith("/" + normalizedRule, StringComparison.OrdinalIgnoreCase))
                return true;
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

    #endregion

    #region Helpers

    private void UpdateUI()
    {
        lstFiles.ItemsSource = null;
        lstFiles.ItemsSource = _files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();

        var hasFiles = _files.Count > 0;
        emptyState.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
        lstFiles.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;

        lblCount.Text = $"{_files.Count} dosya";
    }

    private void AnimateSuccess()
    {
        var originalBrush = dropZone.BorderBrush;
        dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Success color

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        timer.Tick += (s, e) =>
        {
            dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 95));
            timer.Stop();
        };
        timer.Start();
    }

    private static void ShowMessage(string message, string title, MessageBoxImage icon)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
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

    #endregion
}
