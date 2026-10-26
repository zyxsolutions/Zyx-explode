using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZyxExplode;

/// <summary>Linha da tabela de processos.</summary>
public sealed class ProcRow
{
    public string Name { get; init; } = "";
    public int Pid { get; init; }
    public double Cpu { get; init; }
    public long WorkingSet { get; init; }
    public int Threads { get; init; }
    public string Priority { get; init; } = "—";
    public string Affinity { get; init; } = "—";
    public long AffinityMask { get; init; }
    public bool HasRule { get; init; }
    public string CpuText => Cpu >= 0.05 ? $"{Cpu:0.0}%" : "—";
    public string RamText => MemoryAnalyzer.Size((ulong)Math.Max(0, WorkingSet));
    public string RuleText => HasRule ? "regra" : "";
}

/// <summary>Regra salva: sempre que um processo com esse nome abrir, o ZYX EXPLODE aplica prioridade, afinidade e prioridade de disco.</summary>
public sealed class ProcRule
{
    public string Name { get; set; } = "";           // nome do processo sem ".exe"
    public string? Priority { get; set; }            // "idle","below","normal","above","high" ou null (não mexe)
    public long? AffinityMask { get; set; }          // null = não mexe
    public bool LowIo { get; set; }                  // prioridade de disco baixa
    public override string ToString() => Name;

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Priority != null) parts.Add("prioridade " + ProcessManager.PriorityLabel(Priority));
            if (AffinityMask is long m) parts.Add("núcleos " + ProcessManager.CoreList(m));
            if (LowIo) parts.Add("disco baixo");
            return parts.Count == 0 ? "sem ajustes" : string.Join(" · ", parts);
        }
    }
}

public sealed record ProcActionResult(bool Ok, string Message);

/// <summary>
/// Controle de processos no estilo do Process Lasso: lista com uso de CPU, muda prioridade, afinidade (núcleos) e prioridade de disco,
/// e guarda regras que são reaplicadas sempre que o programa abrir de novo. As regras só valem enquanto o ZYX EXPLODE está aberto.
/// </summary>
public static class ProcessManager
{
    // ---------------- Listagem ----------------

    private static readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> _last = new();

    private static readonly string[] Protected =
        { "system", "idle", "registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon", "memory compression", "secure system" };

    public static bool IsProtected(string name) => Protected.Contains(name.ToLowerInvariant());

    public static List<ProcRow> List()
    {
        var rules = LoadRules().Select(r => r.Name.ToLowerInvariant()).ToHashSet();
        var now = DateTime.UtcNow;
        var rows = new List<ProcRow>();
        var seen = new HashSet<int>();
        int cores = Environment.ProcessorCount;

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                double cpu = 0;
                try
                {
                    var t = p.TotalProcessorTime;
                    if (_last.TryGetValue(p.Id, out var prev) && now > prev.At)
                        cpu = (t - prev.Cpu).TotalMilliseconds / (now - prev.At).TotalMilliseconds / cores * 100.0;
                    _last[p.Id] = (t, now);
                    seen.Add(p.Id);
                }
                catch { }

                string prio = "—", aff = "—";
                long mask = 0;
                try { prio = PriorityLabel(p.PriorityClass); } catch { }
                try { mask = (long)p.ProcessorAffinity; aff = AffinityText(mask, cores); } catch { }

                int threads = 0;
                try { threads = p.Threads.Count; } catch { }

                rows.Add(new ProcRow
                {
                    Name = p.ProcessName, Pid = p.Id, Cpu = Math.Max(0, cpu), WorkingSet = SafeWs(p),
                    Threads = threads, Priority = prio, Affinity = aff, AffinityMask = mask, HasRule = rules.Contains(p.ProcessName.ToLowerInvariant()),
                });
            }
            catch { }
            finally { p.Dispose(); }
        }
        foreach (var dead in _last.Keys.Where(k => !seen.Contains(k)).ToList()) _last.Remove(dead);
        return rows.OrderByDescending(r => r.Cpu).ThenByDescending(r => r.WorkingSet).ToList();
    }

    private static long SafeWs(Process p) { try { return p.WorkingSet64; } catch { return 0; } }

    // ---------------- Rótulos ----------------

    public static string PriorityLabel(ProcessPriorityClass c) => c switch
    {
        ProcessPriorityClass.Idle => "Baixíssima",
        ProcessPriorityClass.BelowNormal => "Baixa",
        ProcessPriorityClass.Normal => "Normal",
        ProcessPriorityClass.AboveNormal => "Alta",
        ProcessPriorityClass.High => "Altíssima",
        ProcessPriorityClass.RealTime => "Tempo real",
        _ => c.ToString(),
    };

    public static string PriorityLabel(string key) => PriorityFromKey(key) is ProcessPriorityClass c ? PriorityLabel(c) : key;

    public static ProcessPriorityClass? PriorityFromKey(string? key) => key switch
    {
        "idle" => ProcessPriorityClass.Idle,
        "below" => ProcessPriorityClass.BelowNormal,
        "normal" => ProcessPriorityClass.Normal,
        "above" => ProcessPriorityClass.AboveNormal,
        "high" => ProcessPriorityClass.High,
        _ => null,
    };

    public static string AffinityText(long mask, int cores)
    {
        long all = cores >= 63 ? -1L : (1L << cores) - 1;
        return (mask & all) == all ? "todos" : CoreList(mask);
    }

    /// <summary>"0-3, 6" a partir de uma máscara de bits.</summary>
    public static string CoreList(long mask)
    {
        var ids = new List<int>();
        for (int i = 0; i < 64; i++) if ((mask & (1L << i)) != 0) ids.Add(i);
        if (ids.Count == 0) return "nenhum";
        var parts = new List<string>();
        int start = ids[0], prev = ids[0];
        for (int k = 1; k <= ids.Count; k++)
        {
            if (k < ids.Count && ids[k] == prev + 1) { prev = ids[k]; continue; }
            parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
            if (k < ids.Count) { start = prev = ids[k]; }
        }
        return string.Join(", ", parts);
    }

    // ---------------- Ações ----------------

    public static ProcActionResult SetPriority(int pid, ProcessPriorityClass c)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.PriorityClass = c;
            return new(true, "Prioridade alterada.");
        }
        catch (Exception ex) { return new(false, Reason(ex)); }
    }

    public static ProcActionResult SetAffinity(int pid, long mask)
    {
        if (mask == 0) return new(false, "Escolha pelo menos um núcleo.");
        try
        {
            using var p = Process.GetProcessById(pid);
            p.ProcessorAffinity = (IntPtr)mask;
            return new(true, "Núcleos alterados.");
        }
        catch (Exception ex) { return new(false, Reason(ex)); }
    }

    /// <summary>Prioridade de disco do processo: baixa (1) ou normal (2). A alta exige privilégio extra e não é oferecida.</summary>
    public static ProcActionResult SetLowIo(int pid, bool low)
    {
        IntPtr h = IntPtr.Zero;
        try
        {
            h = OpenProcess(0x0200 /* PROCESS_SET_INFORMATION */, false, pid);
            if (h == IntPtr.Zero) return new(false, "o Windows não deixou abrir o processo");
            int value = low ? 1 : 2;
            int status = NtSetInformationProcess(h, 33 /* ProcessIoPriority */, ref value, sizeof(int));
            return status == 0 ? new(true, "Prioridade de disco alterada.") : new(false, $"o Windows recusou (0x{status:X8})");
        }
        catch (Exception ex) { return new(false, Reason(ex)); }
        finally { if (h != IntPtr.Zero) CloseHandle(h); }
    }

    public static ProcActionResult EndProcess(int pid, bool tree)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (IsProtected(p.ProcessName)) return new(false, "processo essencial do Windows");
            p.Kill(tree);
            return new(true, "Processo encerrado.");
        }
        catch (Exception ex) { return new(false, Reason(ex)); }
    }

    private static string Reason(Exception ex) => ex switch
    {
        ArgumentException => "o processo já foi fechado",
        System.ComponentModel.Win32Exception => "o Windows não permitiu (processo protegido)",
        InvalidOperationException => "o processo já foi fechado",
        _ => ex.Message,
    };

    // ---------------- Regras ----------------

    private static string RulesPath => Path.Combine(AppSettings.Folder, "process-rules.json");
    private static readonly object _lock = new();
    private static List<ProcRule>? _cache;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static List<ProcRule> LoadRules()
    {
        lock (_lock)
        {
            if (_cache != null) return _cache.ToList();
            try
            {
                if (File.Exists(RulesPath))
                    _cache = JsonSerializer.Deserialize<List<ProcRule>>(File.ReadAllText(RulesPath)) ?? new();
            }
            catch { }
            _cache ??= new();
            return _cache.ToList();
        }
    }

    private static void SaveRules(List<ProcRule> rules)
    {
        lock (_lock)
        {
            _cache = rules.ToList();
            try
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.WriteAllText(RulesPath, JsonSerializer.Serialize(rules, Json));
            }
            catch { }
        }
    }

    public static void SaveRule(ProcRule rule)
    {
        var all = LoadRules();
        all.RemoveAll(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase));
        all.Add(rule);
        SaveRules(all.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static void RemoveRule(string name)
    {
        var all = LoadRules();
        all.RemoveAll(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        SaveRules(all);
        _applied.RemoveWhere(k => k.EndsWith("|" + name.ToLowerInvariant()));
    }

    // PID + início do processo + nome, para aplicar cada regra uma vez por processo (o usuário pode mudar na mão depois).
    private static readonly HashSet<string> _applied = new();

    /// <summary>Aplica as regras a processos que ainda não foram tratados. Devolve quantos processos foram ajustados.</summary>
    public static int ApplyRules()
    {
        var rules = LoadRules();
        if (rules.Count == 0) return 0;
        var byName = rules.ToDictionary(r => r.Name.ToLowerInvariant(), r => r);
        int count = 0;
        var alive = new HashSet<string>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName.ToLowerInvariant();
                if (!byName.TryGetValue(name, out var rule)) continue;
                string key = $"{p.Id}|{name}";
                alive.Add(key);
                if (_applied.Contains(key)) continue;
                _applied.Add(key);

                bool any = false;
                if (PriorityFromKey(rule.Priority) is ProcessPriorityClass c)
                    any |= SetPriority(p.Id, c).Ok;
                if (rule.AffinityMask is long m && m != 0)
                    any |= SetAffinity(p.Id, m).Ok;
                if (rule.LowIo)
                    any |= SetLowIo(p.Id, true).Ok;
                if (any) count++;
            }
            catch { }
            finally { p.Dispose(); }
        }
        // esquece processos que fecharam, para que um PID reaproveitado seja tratado de novo
        _applied.RemoveWhere(k => !alive.Contains(k));
        return count;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("ntdll.dll")] private static extern int NtSetInformationProcess(IntPtr h, int infoClass, ref int info, int length);
}
