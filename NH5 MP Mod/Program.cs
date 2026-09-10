using System.Diagnostics;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NH5SetServerUrl;

internal static class Program
{
    const string BackupSuffix = ".nh5revival-backup";
    internal const string RevivalUrl = "http://72.39.41.141:8000/";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    internal static string? FindGameRoot()
    {
        foreach (var root in CandidateGameRoots())
        {
            if (File.Exists(DllFromGameRoot(root)))
                return root;
        }
        return null;
    }

    static IEnumerable<string> CandidateGameRoots()
    {
        yield return @"D:\SteamLibrary\steamapps\common\NASCAR Heat 5";
        yield return @"C:\Program Files (x86)\Steam\steamapps\common\NASCAR Heat 5";
        foreach (var lib in SteamLibraryFolders())
            yield return Path.Combine(lib, "steamapps", "common", "NASCAR Heat 5");
    }

    static IEnumerable<string> SteamLibraryFolders()
    {
        var steam = SteamInstallPath();
        if (string.IsNullOrWhiteSpace(steam) || !Directory.Exists(steam))
            yield break;
        yield return steam;
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
        {
            var p = m.Groups[1].Value.Replace("\\\\", "\\");
            if (Directory.Exists(p))
                yield return p;
        }
    }

    static string? SteamInstallPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    internal static string DllFromGameRoot(string gameRoot) =>
        Path.Combine(gameRoot, "NASCARHeat5_Data", "Managed", "Assembly-CSharp.dll");

    internal static string? ResolveDll(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        var path = input.Trim().Trim('"');
        if (File.Exists(path) && path.EndsWith("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase))
            return path;
        if (Directory.Exists(path))
        {
            var direct = Path.Combine(path, "Assembly-CSharp.dll");
            if (File.Exists(direct))
                return direct;
            var managed = Path.Combine(path, "Managed", "Assembly-CSharp.dll");
            if (File.Exists(managed))
                return managed;
            var fromRoot = DllFromGameRoot(path);
            if (File.Exists(fromRoot))
                return fromRoot;
        }
        return null;
    }

    internal static string BackupPath(string dll) => dll + BackupSuffix;

    internal static bool HeatIsRunning() =>
        Process.GetProcessesByName("NASCARHeat5").Length > 0;

    internal static bool IsRevivalGetBaseUrl(string dll)
    {
        return string.Equals(ReadCurrentUrl(dll), RevivalUrl, StringComparison.OrdinalIgnoreCase);
    }

    internal static string ReadCurrentUrl(string dll)
    {
        using var asm = Read(dll);
        var method = FindGetBaseUrl(asm) ?? throw new InvalidOperationException("This DLL has no NGUtil.GetBaseURL. Point at NASCAR Heat 5\\NASCARHeat5_Data\\Managed.");
        var strings = method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldstr)
            .Select(i => Convert.ToString(i.Operand) ?? "")
            .Where(s => s.Length > 0)
            .ToArray();
        if (strings.Length == 0)
            return "(GetBaseURL has no URL strings)";
        if (strings.Length == 1)
            return strings[0];
        return string.Join("  |  ", strings);
    }

    internal static void Apply(string dll)
    {
        if (IsRevivalGetBaseUrl(dll))
            return;

        var backup = BackupPath(dll);
        if (!File.Exists(backup))
            File.Copy(dll, backup);

        var tmp = dll + ".tmp-seturl";
        if (File.Exists(tmp))
            File.Delete(tmp);

        using (var asm = Read(dll))
        {
            var method = FindGetBaseUrl(asm) ?? throw new InvalidOperationException("This DLL has no NGUtil.GetBaseURL.");
            RewriteGetBaseUrl(method, RevivalUrl);
            asm.Write(tmp);
        }

        File.Copy(tmp, dll, overwrite: true);
        File.Delete(tmp);
    }

    static AssemblyDefinition Read(string dll)
    {
        var managed = Path.GetDirectoryName(dll) ?? ".";
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        var rp = new ReaderParameters { AssemblyResolver = resolver };
        return AssemblyDefinition.ReadAssembly(dll, rp);
    }

    static MethodDefinition? FindGetBaseUrl(AssemblyDefinition asm)
    {
        foreach (var type in asm.MainModule.Types)
        {
            if (!string.Equals(type.Name, "NGUtil", StringComparison.Ordinal))
                continue;
            var m = type.Methods.FirstOrDefault(x =>
                x.Name == "GetBaseURL" && x.Parameters.Count == 0 && x.HasBody);
            if (m != null)
                return m;
        }
        return null;
    }

    static void RewriteGetBaseUrl(MethodDefinition method, string url)
    {
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldstr, url);
        il.Emit(OpCodes.Ret);
        method.Body.MaxStackSize = 1;
    }
}

internal sealed class MainForm : Form
{
    readonly TextBox _game;
    readonly Label _status;
    readonly Button _install;

    public MainForm()
    {
        Text = "NH5 MP Mod";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 640;
        Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var intro = new Label
        {
            Left = 16,
            Top = 14,
            Width = 590,
            Height = 36,
            Text = "Close NASCAR Heat 5 first. If your game folder is not found, browse to it, then click Install."
        };
        Controls.Add(intro);

        Controls.Add(new Label { Left = 16, Top = 62, Width = 80, Text = "Game folder" });
        _game = new TextBox { Left = 100, Top = 58, Width = 390 };
        Controls.Add(_game);
        var browse = new Button { Left = 500, Top = 56, Width = 100, Text = "Browse..." };
        browse.Click += (_, _) => Browse();
        Controls.Add(browse);

        _install = new Button { Left = 100, Top = 100, Width = 160, Height = 32, Text = "Install" };
        _install.Click += (_, _) => RunSafe(Install);
        Controls.Add(_install);

        _status = new Label { Left = 16, Top = 146, Width = 590, Height = 28, Text = "" };
        Controls.Add(_status);

        var found = Program.FindGameRoot();
        if (found != null)
            _game.Text = found;
        Shown += (_, _) => BeginInvoke(TryAutoInstall);
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select the NASCAR Heat 5 folder, NASCARHeat5_Data, or Managed",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (!string.IsNullOrWhiteSpace(_game.Text) && Directory.Exists(_game.Text))
            dlg.SelectedPath = _game.Text;
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        _game.Text = dlg.SelectedPath;
    }

    string RequireDll()
    {
        return Program.ResolveDll(_game.Text)
            ?? throw new InvalidOperationException("Could not find Assembly-CSharp.dll. Browse to NASCAR Heat 5, NASCARHeat5_Data, or Managed.");
    }

    void TryAutoInstall()
    {
        if (Program.ResolveDll(_game.Text) == null)
        {
            _status.Text = "Browse to your NASCAR Heat 5 folder, then click Install.";
            return;
        }
        RunSafe(Install);
    }

    void Install()
    {
        if (Program.HeatIsRunning())
            throw new InvalidOperationException("Close NASCAR Heat 5 first, then run NH5 MP Mod again.");
        var dll = RequireDll();
        if (Program.IsRevivalGetBaseUrl(dll))
        {
            Done("NH5 MP Mod is already installed.");
            return;
        }
        Program.Apply(dll);
        Done("NH5 MP Mod finished installing.");
    }

    void Done(string message)
    {
        MessageBox.Show(this, message, "NH5 MP Mod", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Application.Exit();
    }

    void RunSafe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "NH5 MP Mod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

