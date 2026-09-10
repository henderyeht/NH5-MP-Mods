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

    internal static bool IsYearSplitGetBaseUrl(string dll)
    {
        var strings = ReadUrlStrings(dll);
        return strings.Any(s => string.Equals(s, RevivalPresentUrl, StringComparison.OrdinalIgnoreCase))
            && strings.Any(s => string.Equals(s, RevivalFutureUrl, StringComparison.OrdinalIgnoreCase));
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
        if (IsYearSplitGetBaseUrl(dll))
            return;

        var stock = FindStockSource(dll);
        var backup = BackupPath(dll);
        if (!File.Exists(backup) && HasStockYearUrls(dll))
            File.Copy(dll, backup);

        var tmp = dll + ".tmp-seturl";
        if (File.Exists(tmp))
            File.Delete(tmp);

        using (var live = Read(dll))
        {
            var dest = FindGetBaseUrl(live) ?? throw new InvalidOperationException("This DLL has no NGUtil.GetBaseURL.");
            if (stock != null && !string.Equals(stock, dll, StringComparison.OrdinalIgnoreCase))
            {
                using var stockAsm = Read(stock);
                var src = FindGetBaseUrl(stockAsm) ?? throw new InvalidOperationException("The stock backup has no NGUtil.GetBaseURL.");
                CopyMethodBody(src, dest);
            }
            if (stock != null)
                ReplaceYearUrls(dest);
            else
                EmitYearSplitGetBaseUrl(dest);
            if (!HasRevivalYearUrls(dest))
                throw new InvalidOperationException("GetBaseURL did not keep both year URLs.");
            live.Write(tmp);
        }

        File.Copy(tmp, dll, overwrite: true);
        try { File.Delete(tmp); }
        catch (IOException) { }
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

internal sealed class MainForm : Form
{
    readonly TextBox _game;
    readonly Label _status;
    readonly Button _install;

    public MainForm()
    {
        Text = "NH5 MP Final Release";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 640;
        Height = 236;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var intro = new Label
        {
            Left = 16,
            Top = 14,
            Width = 590,
            Height = 48,
            Text = "Close NASCAR Heat 5 first. This keeps 2020 on /n2020/ and 2022 on /n2022/. If your game folder is not found, browse to it, then click Install."
        };
        Controls.Add(intro);

        Controls.Add(new Label { Left = 16, Top = 70, Width = 80, Text = "Game folder" });
        _game = new TextBox { Left = 100, Top = 66, Width = 390 };
        Controls.Add(_game);
        var browse = new Button { Left = 500, Top = 64, Width = 100, Text = "Browse..." };
        browse.Click += (_, _) => Browse();
        Controls.Add(browse);

        _install = new Button { Left = 100, Top = 108, Width = 160, Height = 32, Text = "Install" };
        _install.Click += (_, _) => RunSafe(Install);
        Controls.Add(_install);

        _status = new Label { Left = 16, Top = 154, Width = 590, Height = 36, Text = "" };
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
            throw new InvalidOperationException("Close NASCAR Heat 5 first, then run NH5 MP Final Release again.");
        var dll = RequireDll();
        if (Program.IsYearSplitGetBaseUrl(dll))
        {
            Done("NH5 MP Final Release is already installed.");
            return;
        }
        Program.Apply(dll);
        Done("NH5 MP Final Release finished installing.");
    }

    void Done(string message)
    {
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
