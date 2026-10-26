using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace ZyxExplode;

/// <summary>Foto da memória física: tudo em bytes. As listas (livre, standby, modificada) vêm do próprio Windows.</summary>
public sealed record MemorySnapshot(
    ulong PageSize, ulong TotalPhysical, ulong Available,
    ulong Zeroed, ulong Free, ulong Modified, ulong ModifiedNoWrite, ulong Bad,
    ulong[] StandbyByPriority, bool ListsAvailable,
    ulong SystemCache, ulong KernelPaged, ulong KernelNonpaged,
    ulong CommitTotal, ulong CommitLimit, ulong CommitPeak,
    uint Handles, uint Processes, uint Threads)
{
    public ulong Standby => StandbyByPriority.Aggregate(0UL, (a, b) => a + b);
    public ulong FreeAndZero => Zeroed + Free;

    /// <summary>Em uso pelos programas e pelo sistema = o que sobra depois de tirar as listas que o Windows pode reaproveitar.</summary>
    public ulong InUse
    {
        get
        {
            if (!ListsAvailable) return TotalPhysical > Available ? TotalPhysical - Available : 0;
            ulong other = FreeAndZero + Standby + Modified + ModifiedNoWrite + Bad;
            return TotalPhysical > other ? TotalPhysical - other : 0;
        }
    }
}

public sealed record MemoryActionResult(bool Ok, string Message, long FreedBytes);

public enum MemoryAction { WorkingSets, SystemWorkingSet, ModifiedList, StandbyList, StandbyLowPriority }

/// <summary>Linha da tabela de processos (working set = o que o processo ocupa agora na RAM).</summary>
public sealed class ProcessMemRow
{
    public string Name { get; init; } = "";
    public int Pid { get; init; }
    public long WorkingSet { get; init; }
    public long Private { get; init; }
    public long Peak { get; init; }
    public string WorkingSetText => MemoryAnalyzer.Size((ulong)Math.Max(0, WorkingSet));
    public string PrivateText => MemoryAnalyzer.Size((ulong)Math.Max(0, Private));
    public string PeakText => MemoryAnalyzer.Size((ulong)Math.Max(0, Peak));
}

/// <summary>
/// Análise da memória no estilo do RAMMap: quanto da RAM está em uso, em cache (standby), esperando para ir ao disco (modificada)
/// ou livre, e ações para esvaziar cada lista. Usa a API do Windows (NtQuerySystemInformation/NtSetSystemInformation,
/// a mesma que ferramentas como o ISLC e o próprio RAMMap usam); exige rodar como administrador.
/// </summary>
public static class MemoryAnalyzer
{
    private static bool _profilePrivilege;

    private static void EnsureProfilePrivilege()
    {
        if (_profilePrivilege) return;
        Optimizer.EnablePrivilege("SeProfileSingleProcessPrivilege");
        _profilePrivilege = true;
    }

    private const int SystemMemoryListInformation = 80;
    private const int SystemFileCacheInformation = 21;

    public static string Size(ulong bytes)
    {
        double mb = bytes / 1048576.0;
        return mb >= 1024 ? $"{mb / 1024:0.00} GB" : $"{mb:N0} MB";
    }

    public static MemorySnapshot Snapshot()
    {
        if (!GetPerformanceInfo(out var pi, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        ulong page = (ulong)pi.PageSize;
        ulong zero = 0, free = 0, modified = 0, modifiedNoWrite = 0, bad = 0;
        var standby = new ulong[8];
        bool lists = false;

        try
        {
            EnsureProfilePrivilege();
            // SYSTEM_MEMORY_LIST_INFORMATION: 5 contadores, 8 de standby por prioridade, 8 de páginas reaproveitadas, 1 de modificadas no arquivo de paginação.
            const int count = 22;
            IntPtr buf = Marshal.AllocHGlobal(count * 8);
            try
            {
                int status = NtQuerySystemInformation(SystemMemoryListInformation, buf, count * 8, out _);
                if (status == 0)
                {
                    zero = (ulong)Marshal.ReadInt64(buf, 0 * 8) * page;
                    free = (ulong)Marshal.ReadInt64(buf, 1 * 8) * page;
                    modified = (ulong)Marshal.ReadInt64(buf, 2 * 8) * page;
                    modifiedNoWrite = (ulong)Marshal.ReadInt64(buf, 3 * 8) * page;
                    bad = (ulong)Marshal.ReadInt64(buf, 4 * 8) * page;
                    for (int i = 0; i < 8; i++) standby[i] = (ulong)Marshal.ReadInt64(buf, (5 + i) * 8) * page;
                    lists = true;
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { }

        return new MemorySnapshot(
            page, (ulong)pi.PhysicalTotal * page, (ulong)pi.PhysicalAvailable * page,
            zero, free, modified, modifiedNoWrite, bad, standby, lists,
            (ulong)pi.SystemCache * page, (ulong)pi.KernelPaged * page, (ulong)pi.KernelNonpaged * page,
            (ulong)pi.CommitTotal * page, (ulong)pi.CommitLimit * page, (ulong)pi.CommitPeak * page,
            pi.HandleCount, pi.ProcessCount, pi.ThreadCount);
    }

    /// <summary>Páginas lidas do disco por segundo (falhas graves). Valor alto e constante = a RAM não está dando conta.</summary>
    public static long? HardFaultsPerSecond()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT PagesInputPersec FROM Win32_PerfFormattedData_PerfOS_Memory");
            foreach (ManagementObject mo in s.Get())
                return Convert.ToInt64(mo["PagesInputPersec"]);
        }
        catch { }
        return null;
    }

    public static List<ProcessMemRow> Processes()
    {
        var list = new List<ProcessMemRow>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                list.Add(new ProcessMemRow
                {
                    Name = p.ProcessName, Pid = p.Id,
                    WorkingSet = p.WorkingSet64, Private = p.PrivateMemorySize64, Peak = p.PeakWorkingSet64,
                });
            }
            catch { }
            finally { p.Dispose(); }
        }
        return list.OrderByDescending(r => r.WorkingSet).ToList();
    }

    /// <summary>Esvazia o working set de um processo (o Windows traz de volta só o que ele usar de novo).</summary>
    public static bool TrimProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return EmptyWorkingSet(p.Handle);
        }
        catch { return false; }
    }

    public static MemoryActionResult Run(MemoryAction action)
    {
        long before = (long)Snapshot().Available;
        int status;
        try
        {
            switch (action)
            {
                case MemoryAction.WorkingSets: status = SetList(2); break;           // MemoryEmptyWorkingSets
                case MemoryAction.ModifiedList: status = SetList(3); break;          // MemoryFlushModifiedList
                case MemoryAction.StandbyList: status = SetList(4); break;           // MemoryPurgeStandbyList
                case MemoryAction.StandbyLowPriority: status = SetList(5); break;    // MemoryPurgeLowPriorityStandbyList
                case MemoryAction.SystemWorkingSet: status = FlushSystemWorkingSet(); break;
                default: return new MemoryActionResult(false, "Ação desconhecida.", 0);
            }
        }
        catch (Exception ex) { return new MemoryActionResult(false, ex.Message, 0); }

        if (status != 0)
            return new MemoryActionResult(false, $"o Windows recusou (0x{status:X8}); abra o ZYX EXPLODE como administrador", 0);

        Thread.Sleep(400); // dá tempo para o Windows atualizar os contadores
        long after = (long)Snapshot().Available;
        return new MemoryActionResult(true, "Concluído.", after - before);
    }

    private static int SetList(int command)
    {
        EnsureProfilePrivilege();
        int cmd = command;
        return NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
    }

    /// <summary>Esvazia o working set do sistema (cache de arquivos do kernel): mínimo e máximo = -1, como o RAMMap.</summary>
    private static int FlushSystemWorkingSet()
    {
        Optimizer.EnablePrivilege("SeIncreaseQuotaPrivilege");
        const int size = 64; // SYSTEM_FILECACHE_INFORMATION (x64)
        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            for (int i = 0; i < size; i += 8) Marshal.WriteInt64(buf, i, 0);
            Marshal.WriteInt64(buf, 24, -1); // MinimumWorkingSet
            Marshal.WriteInt64(buf, 32, -1); // MaximumWorkingSet
            return NtSetSystemInformationPtr(SystemFileCacheInformation, buf, size);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Leitura em português do estado da RAM, para o técnico explicar ao cliente.</summary>
    public static List<string> Diagnose(MemorySnapshot s, long? hardFaults)
    {
        var list = new List<string>();
        if (s.TotalPhysical == 0) return list;
        double availPct = s.Available * 100.0 / s.TotalPhysical;
        double commitPct = s.CommitLimit > 0 ? s.CommitTotal * 100.0 / s.CommitLimit : 0;

        if (availPct < 8) list.Add($"RAM quase cheia: só {availPct:0}% disponível. Feche programas pesados ou considere mais memória.");
        else if (availPct < 20) list.Add($"RAM com pouca folga ({availPct:0}% disponível).");
        else list.Add($"RAM com folga: {availPct:0}% disponível.");

        if (s.ListsAvailable)
        {
            double standbyPct = s.Standby * 100.0 / s.TotalPhysical;
            if (standbyPct > 40)
                list.Add($"A lista de espera (standby) ocupa {standbyPct:0}% da RAM. É cache e o Windows libera sozinho, mas esvaziá-la ajuda quando um jogo ou programa engasga ao abrir.");
            if (s.Modified > s.TotalPhysical / 10)
                list.Add($"Há {Size(s.Modified)} de páginas modificadas esperando para ir ao disco. Disco lento pode causar travadas.");
        }
        else list.Add("Não foi possível ler as listas da RAM. Rode o programa como administrador para ver standby, modificada e livre.");

        if (commitPct > 85) list.Add($"Memória comprometida em {commitPct:0}% do limite (RAM + arquivo de paginação). Risco de erros de memória insuficiente.");
        if (hardFaults is long h && h > 200) list.Add($"{h:N0} páginas lidas do disco por segundo: o sistema está usando o arquivo de paginação com frequência.");
        if (s.KernelNonpaged > 2UL * 1024 * 1024 * 1024) list.Add($"Pool não paginado de {Size(s.KernelNonpaged)}: valor alto pode indicar vazamento de memória em um driver.");
        return list;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public nuint CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }

    [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pi, uint size);
    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);
    [DllImport("ntdll.dll", EntryPoint = "NtSetSystemInformation")] private static extern int NtSetSystemInformationPtr(int infoClass, IntPtr info, int length);
}
