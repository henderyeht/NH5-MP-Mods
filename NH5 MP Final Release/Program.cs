using System.Diagnostics;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NH5MpFinalRelease;

internal static class Program
{
    const string BackupSuffix = ".nh5final-backup";
    internal const string RevivalUrl = "http://72.39.41.141:8000/";
    internal const string RevivalPresentUrl = "http://72.39.41.141:8000/n2020/";
    internal const string RevivalFutureUrl = "http://72.39.41.141:8000/n2022/";
    const string StockPresentUrl = "https://n2020.mgrsys.com/";
    const string StockFutureUrl = "https://n2022.mgrsys.com/";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[0], "--apply", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var dll = ResolveDll(args[1]) ?? throw new InvalidOperationException("Could not find Assembly-CSharp.dll.");
                if (HeatIsRunning())
                    throw new InvalidOperationException("Close NASCAR Heat 5 first, then run NH5 MP Final Release again.");
                Apply(dll);
                Console.WriteLine(ReadCurrentUrl(dll));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }

    internal static string[] FindAllGameRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var root in CandidateGameRoots())
        {
            try
            {
                if (!Directory.Exists(root))
                    continue;
                var full = Path.GetFullPath(root);
                if (!seen.Add(full))
                    continue;
                if (File.Exists(DllFromGameRoot(full)))
                    list.Add(full);
            }
            catch
            {
            }
        }
        return list.ToArray();
    }

    internal static string? FindGameRoot()
    {
        var all = FindAllGameRoots();
        return all.Length == 0 ? null : all[0];
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

    internal static bool IsYearSplitGetBaseUrl(string dll)
    {
        var strings = ReadUrlStrings(dll);
        return strings.Any(s => string.Equals(s, RevivalPresentUrl, StringComparison.OrdinalIgnoreCase))
            && strings.Any(s => string.Equals(s, RevivalFutureUrl, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsAlreadyInstalled(string dll)
    {
        if (IsYearSplitGetBaseUrl(dll))
            return true;
        var strings = ReadUrlStrings(dll);
        return strings.Any(s => string.Equals(s, RevivalPresentUrl, StringComparison.OrdinalIgnoreCase))
            && !CanYearSplit(dll);
    }

    internal static bool CanYearSplit(string dll)
    {
        if (HasStockYearUrls(dll))
            return true;
        var stock = FindStockSource(dll);
        if (stock != null
            && !string.Equals(stock, dll, StringComparison.OrdinalIgnoreCase)
            && HasStockYearUrls(stock))
            return true;
        try
        {
            using var asm = Read(dll);
            return HasFutureYearApi(asm.MainModule);
        }
        catch
        {
            return false;
        }
    }

    static bool HasFutureYearApi(ModuleDefinition module)
    {
        foreach (var type in AllTypes(module))
        {
            if (!string.Equals(type.Name, "GameYearManager", StringComparison.Ordinal))
                continue;
            if (type.Methods.Any(m => m.Name == "IsFutureYearActive"))
                return true;
        }
        return FindMethodRef(module, mr => mr.Name == "IsFutureYearActive" && mr.DeclaringType.Name == "GameYearManager") != null
            && FindMethodRef(module, mr =>
                mr.Name == "get_Instance"
                && mr.DeclaringType.FullName.Contains("GameYearManager", StringComparison.Ordinal)) != null;
    }

    internal static string DescribeInstall(string gameRoot)
    {
        var dll = DllFromGameRoot(gameRoot);
        try
        {
            var year = CanYearSplit(dll) ? "2020+2022" : "2020 only";
            var tag = IsAlreadyInstalled(dll) ? "patched" : "needs patch";
            return year + " | " + tag + " | " + gameRoot;
        }
        catch (Exception ex)
        {
            return gameRoot + " (" + ex.Message + ")";
        }
    }

    internal static string ReadCurrentUrl(string dll)
    {
        var strings = ReadUrlStrings(dll);
        if (strings.Length == 0)
            return "(GetBaseURL has no URL strings)";
        if (strings.Length == 1)
            return strings[0];
        return string.Join("  |  ", strings);
    }

    static string[] ReadUrlStrings(string dll)
    {
        using var asm = Read(dll);
        var method = FindGetBaseUrl(asm) ?? throw new InvalidOperationException("This DLL has no NGUtil.GetBaseURL. Point at NASCAR Heat 5\\NASCARHeat5_Data\\Managed.");
        return UrlStrings(method);
    }

    static string[] UrlStrings(MethodDefinition method)
    {
        return method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldstr)
            .Select(i => Convert.ToString(i.Operand) ?? "")
            .Where(s => s.Length > 0)
            .ToArray();
    }

    static bool HasStockYearUrls(string dll)
    {
        var strings = ReadUrlStrings(dll);
        return strings.Any(s => string.Equals(s, StockPresentUrl, StringComparison.OrdinalIgnoreCase))
            && strings.Any(s => string.Equals(s, StockFutureUrl, StringComparison.OrdinalIgnoreCase));
    }

    static string? FindStockSource(string dll)
    {
        if (HasStockYearUrls(dll))
            return dll;
        var dir = Path.GetDirectoryName(dll) ?? ".";
        var name = Path.GetFileName(dll);
        foreach (var candidate in new[]
        {
            Path.Combine(dir, name + ".stock-backup"),
            Path.Combine(dir, name + ".nh5revival-backup"),
            Path.Combine(dir, name + BackupSuffix),
            dll + ".stock-backup",
            dll + ".nh5revival-backup",
        })
        {
            if (File.Exists(candidate) && HasStockYearUrls(candidate))
                return candidate;
        }
        return null;
    }

    internal static void Apply(string dll)
    {
        if (IsAlreadyInstalled(dll))
            return;

        var stock = FindStockSource(dll);
        var backup = BackupPath(dll);
        if (!File.Exists(backup))
            File.Copy(dll, backup);

        var tmp = dll + ".tmp-seturl";
        if (File.Exists(tmp))
            File.Delete(tmp);

        using (var live = Read(dll))
        {
            var dest = FindGetBaseUrl(live) ?? throw new InvalidOperationException("This DLL has no NGUtil.GetBaseURL.");
            var yearSplit = false;
            if (stock != null && !string.Equals(stock, dll, StringComparison.OrdinalIgnoreCase))
            {
                using var stockAsm = Read(stock);
                var src = FindGetBaseUrl(stockAsm) ?? throw new InvalidOperationException("The stock backup has no NGUtil.GetBaseURL.");
                CopyMethodBody(src, dest);
            }
            if (stock != null)
            {
                ReplaceYearUrls(dest);
                yearSplit = HasRevivalYearUrls(dest);
            }
            if (!yearSplit && HasFutureYearApi(live.MainModule))
            {
                try
                {
                    EmitYearSplitGetBaseUrl(dest);
                    yearSplit = HasRevivalYearUrls(dest);
                }
                catch (InvalidOperationException)
                {
                    yearSplit = false;
                }
            }
            if (!yearSplit)
                RewritePresentOnly(dest);
            if (!HasRevivalPresent(dest))
                throw new InvalidOperationException("GetBaseURL did not keep the 2020 revival URL.");
            live.Write(tmp);
        }

        File.Copy(tmp, dll, overwrite: true);
        try { File.Delete(tmp); }
        catch (IOException) { }
    }

    static void RewritePresentOnly(MethodDefinition method)
    {
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldstr, RevivalPresentUrl);
        il.Emit(OpCodes.Ret);
        method.Body.MaxStackSize = 1;
    }

    static bool HasRevivalPresent(MethodDefinition method)
    {
        return UrlStrings(method).Any(s => string.Equals(s, RevivalPresentUrl, StringComparison.OrdinalIgnoreCase));
    }

    static bool HasRevivalYearUrls(MethodDefinition method)
    {
        var strings = UrlStrings(method);
        return strings.Any(s => string.Equals(s, RevivalPresentUrl, StringComparison.OrdinalIgnoreCase))
            && strings.Any(s => string.Equals(s, RevivalFutureUrl, StringComparison.OrdinalIgnoreCase));
    }

    static void EmitYearSplitGetBaseUrl(MethodDefinition method)
    {
        var module = method.Module;
        var global = FindMethodRef(module, mr => mr.Name == "Global" && mr.DeclaringType.Name == "InputSys")
            ?? throw new InvalidOperationException("Could not find InputSys.Global in this DLL.");
        var keyDown = FindMethodRef(module, mr => mr.Name == "Debug_GetKeyDown")
            ?? throw new InvalidOperationException("Could not find IInputSystem.Debug_GetKeyDown in this DLL.");
        var instance = FindMethodRef(module, mr =>
                mr.Name == "get_Instance"
                && mr.DeclaringType.FullName.Contains("LazySingleton", StringComparison.Ordinal)
                && mr.DeclaringType.FullName.Contains("GameYearManager", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Could not find LazySingleton<GameYearManager>.Instance in this DLL.");
        var future = FindMethodRef(module, mr => mr.Name == "IsFutureYearActive" && mr.DeclaringType.Name == "GameYearManager")
            ?? throw new InvalidOperationException("Could not find GameYearManager.IsFutureYearActive in this DLL.");

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = false;
        method.Body.MaxStackSize = 8;

        var il = method.Body.GetILProcessor();
        var callInstance = il.Create(OpCodes.Call, instance);
        var ldPresent = il.Create(OpCodes.Ldstr, RevivalPresentUrl);
        var ret = il.Create(OpCodes.Ret);

        il.Emit(OpCodes.Call, global);
        il.Emit(OpCodes.Ldc_I4_S, (sbyte)42);
        il.Emit(OpCodes.Callvirt, keyDown);
        il.Emit(OpCodes.Brfalse, callInstance);
        il.Emit(OpCodes.Ldstr, "http://192.168.1.132:8000/");
        il.Emit(OpCodes.Ret);
        il.Append(callInstance);
        il.Emit(OpCodes.Callvirt, future);
        il.Emit(OpCodes.Brfalse, ldPresent);
        il.Emit(OpCodes.Ldstr, RevivalFutureUrl);
        il.Emit(OpCodes.Br, ret);
        il.Append(ldPresent);
        il.Append(ret);
    }

    static MethodReference? FindMethodRef(ModuleDefinition module, Func<MethodReference, bool> match)
    {
        foreach (var type in AllTypes(module))
        {
            foreach (var m in type.Methods)
            {
                if (!m.HasBody)
                    continue;
                foreach (var inst in m.Body.Instructions)
                {
                    if (inst.Operand is MethodReference mr && match(mr))
                        return mr;
                }
            }
        }
        return null;
    }

    static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
    {
        var stack = new Stack<TypeDefinition>(module.Types);
        while (stack.Count > 0)
        {
            var type = stack.Pop();
            yield return type;
            foreach (var nested in type.NestedTypes)
                stack.Push(nested);
        }
    }

    static void ReplaceYearUrls(MethodDefinition method)
    {
        foreach (var inst in method.Body.Instructions)
        {
            if (inst.OpCode != OpCodes.Ldstr)
                continue;
            var s = Convert.ToString(inst.Operand) ?? "";
            if (string.Equals(s, StockPresentUrl, StringComparison.OrdinalIgnoreCase))
                inst.Operand = RevivalPresentUrl;
            else if (string.Equals(s, StockFutureUrl, StringComparison.OrdinalIgnoreCase))
                inst.Operand = RevivalFutureUrl;
        }
    }

    static void CopyMethodBody(MethodDefinition src, MethodDefinition dest)
    {
        dest.Body.ExceptionHandlers.Clear();
        dest.Body.Variables.Clear();
        dest.Body.Instructions.Clear();
        dest.Body.InitLocals = src.Body.InitLocals;
        dest.Body.MaxStackSize = src.Body.MaxStackSize;

        var module = dest.Module;
        foreach (var v in src.Body.Variables)
            dest.Body.Variables.Add(new VariableDefinition(module.ImportReference(v.VariableType)));

        var map = new Dictionary<Instruction, Instruction>();
        var il = dest.Body.GetILProcessor();
        foreach (var inst in src.Body.Instructions)
        {
            var clone = CloneInstruction(inst, module);
            map[inst] = clone;
            il.Append(clone);
        }

        foreach (var inst in dest.Body.Instructions)
            inst.Operand = RemapOperand(inst.Operand, map);

        foreach (var eh in src.Body.ExceptionHandlers)
        {
            dest.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
            {
                CatchType = eh.CatchType == null ? null : module.ImportReference(eh.CatchType),
                TryStart = map[eh.TryStart],
                TryEnd = map[eh.TryEnd],
                HandlerStart = map[eh.HandlerStart],
                HandlerEnd = map[eh.HandlerEnd],
                FilterStart = eh.FilterStart == null ? null : map[eh.FilterStart],
            });
        }
    }

    static Instruction CloneInstruction(Instruction src, ModuleDefinition module)
    {
        var clone = Instruction.Create(OpCodes.Nop);
        clone.OpCode = src.OpCode;
        clone.Operand = ImportOperand(src.Operand, module);
        return clone;
    }

    static object? ImportOperand(object? operand, ModuleDefinition module)
    {
        return operand switch
        {
            MethodReference method => module.ImportReference(method),
            FieldReference field => module.ImportReference(field),
            TypeReference type => module.ImportReference(type),
            _ => operand,
        };
    }

    static object? RemapOperand(object? operand, Dictionary<Instruction, Instruction> map)
    {
        if (operand is Instruction target)
            return map[target];
        if (operand is Instruction[] targets)
            return targets.Select(t => map[t]).ToArray();
        return operand;
    }

    static AssemblyDefinition Read(string dll)
    {
        var managed = Path.GetDirectoryName(dll) ?? ".";
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managed);
        foreach (var extra in ExtraResolveDirs(managed))
            resolver.AddSearchDirectory(extra);
        var rp = new ReaderParameters { AssemblyResolver = resolver };
        return AssemblyDefinition.ReadAssembly(dll, rp);
    }

    static IEnumerable<string> ExtraResolveDirs(string managed)
    {
        if (File.Exists(Path.Combine(managed, "UnityEngine.CoreModule.dll")))
            yield break;
        foreach (var root in CandidateGameRoots())
        {
            var dir = Path.Combine(root, "NASCARHeat5_Data", "Managed");
            if (Directory.Exists(dir))
                yield return dir;
        }
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
}

internal sealed class GameInstall
{
    public string Root { get; }
    public string Label { get; }
    public GameInstall(string root, string label)
    {
        Root = root;
        Label = label;
    }
    public override string ToString() => Label;
}

internal sealed class MainForm : Form
{
    readonly ListBox _installs;
    readonly Label _status;
    readonly Button _install;
    readonly Button _installAll;

    public MainForm()
    {
        Text = "NH5 MP Final Release";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 720;
        Height = 360;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var intro = new Label
        {
            Left = 16,
            Top = 12,
            Width = 670,
            Height = 48,
            Text = "Close NASCAR Heat 5 first. Pick which copy to patch if you have more than one. A 2020-only install (no Next Gen in Assembly-CSharp) still gets /n2020/ so Challenges work."
        };
        Controls.Add(intro);

        Controls.Add(new Label { Left = 16, Top = 64, Width = 80, Text = "Installs" });
        _installs = new ListBox { Left = 100, Top = 64, Width = 500, Height = 120 };
        Controls.Add(_installs);
        var browse = new Button { Left = 610, Top = 64, Width = 80, Height = 28, Text = "Browse..." };
        browse.Click += (_, _) => RunSafe(Browse);
        Controls.Add(browse);

        _install = new Button { Left = 100, Top = 196, Width = 160, Height = 32, Text = "Install selected" };
        _install.Click += (_, _) => RunSafe(InstallSelected);
        Controls.Add(_install);

        _installAll = new Button { Left = 272, Top = 196, Width = 160, Height = 32, Text = "Install all" };
        _installAll.Click += (_, _) => RunSafe(InstallAll);
        Controls.Add(_installAll);

        _status = new Label { Left = 16, Top = 244, Width = 670, Height = 64, Text = "" };
        Controls.Add(_status);

        Shown += (_, _) => BeginInvoke(TryAutoInstall);
    }

    void RefreshList(string? selectRoot = null)
    {
        var keep = selectRoot;
        var items = new List<GameInstall>();
        foreach (var root in Program.FindAllGameRoots())
            items.Add(new GameInstall(root, Program.DescribeInstall(root)));
        if (!string.IsNullOrWhiteSpace(keep)
            && items.All(i => !string.Equals(i.Root, keep, StringComparison.OrdinalIgnoreCase)))
        {
            var dll = Program.ResolveDll(keep);
            if (dll != null)
            {
                var root = GameRootFromDll(dll);
                items.Add(new GameInstall(root, Program.DescribeInstall(root)));
            }
        }
        _installs.Items.Clear();
        foreach (var item in items)
            _installs.Items.Add(item);
        if (_installs.Items.Count == 0)
            return;
        var idx = 0;
        if (!string.IsNullOrWhiteSpace(keep))
        {
            for (var i = 0; i < _installs.Items.Count; i++)
            {
                if (_installs.Items[i] is GameInstall gi
                    && string.Equals(gi.Root, keep, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
        }
        _installs.SelectedIndex = idx;
    }

    static string GameRootFromDll(string dll)
    {
        var managed = Path.GetDirectoryName(dll) ?? ".";
        var data = Path.GetDirectoryName(managed) ?? managed;
        var root = Path.GetDirectoryName(data);
        return string.IsNullOrWhiteSpace(root) ? managed : root;
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select a NASCAR Heat 5 folder, NASCARHeat5_Data, or Managed",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (_installs.SelectedItem is GameInstall selected && Directory.Exists(selected.Root))
            dlg.SelectedPath = selected.Root;
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        var dll = Program.ResolveDll(dlg.SelectedPath)
            ?? throw new InvalidOperationException("Could not find Assembly-CSharp.dll in that folder.");
        RefreshList(GameRootFromDll(dll));
    }

    void TryAutoInstall()
    {
        RefreshList();
        if (_installs.Items.Count == 0)
        {
            _status.Text = "No NASCAR Heat 5 folder was found. Browse to a copy, then click Install selected.";
            return;
        }
        if (_installs.Items.Count == 1)
        {
            RunSafe(InstallSelected);
            return;
        }
        _status.Text = "More than one copy was found. Select one and click Install selected, or Install all.";
    }

    void InstallSelected()
    {
        if (Program.HeatIsRunning())
            throw new InvalidOperationException("Close NASCAR Heat 5 first, then run NH5 MP Final Release again.");
        if (_installs.SelectedItem is not GameInstall item)
            throw new InvalidOperationException("Select a game folder, or Browse to one.");
        PatchOne(item.Root);
        RefreshList(item.Root);
        FinishIfDone("NH5 MP Final Release finished installing that copy.");
    }

    void InstallAll()
    {
        if (Program.HeatIsRunning())
            throw new InvalidOperationException("Close NASCAR Heat 5 first, then run NH5 MP Final Release again.");
        if (_installs.Items.Count == 0)
            throw new InvalidOperationException("Browse to a NASCAR Heat 5 folder first.");
        var roots = _installs.Items.Cast<GameInstall>().Select(i => i.Root).ToArray();
        foreach (var root in roots)
            PatchOne(root);
        RefreshList();
        FinishIfDone("NH5 MP Final Release finished installing every listed copy.");
    }

    static void PatchOne(string gameRoot)
    {
        var dll = Program.DllFromGameRoot(gameRoot);
        if (!File.Exists(dll))
            throw new InvalidOperationException("Could not find Assembly-CSharp.dll in " + gameRoot);
        if (Program.IsAlreadyInstalled(dll))
            return;
        Program.Apply(dll);
    }

    void FinishIfDone(string message)
    {
        var remaining = _installs.Items.Cast<GameInstall>().Any(i =>
        {
            try { return !Program.IsAlreadyInstalled(Program.DllFromGameRoot(i.Root)); }
            catch { return true; }
        });
        _status.Text = message;
        if (remaining)
        {
            _status.Text += " Other copies still need a patch.";
            return;
        }
        MessageBox.Show(this, message, "NH5 MP Final Release", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(this, ex.Message, "NH5 MP Final Release", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
