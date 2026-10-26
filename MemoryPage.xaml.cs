using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ZyxExplode;

/// <summary>Linha do gráfico de standby por prioridade.</summary>
public sealed class PriRow
{
    public string Label { get; init; } = "";
    public double Fraction { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>ZYX FERRAMENTAS · Memória: análise da RAM no estilo do RAMMap, com ações para esvaziar cada lista.</summary>
public partial class MemoryPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _busy, _actionBusy;

    public MemoryPage()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => { if (ChkAuto.IsChecked == true) await RefreshSummaryAsync(); };
    }

    private async void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            _timer.Start();
            await RefreshSummaryAsync();
            await RefreshProcessesAsync();
        }
        else _timer.Stop();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSummaryAsync();
        await RefreshProcessesAsync();
    }

    // ---------------- Leitura ----------------

    private async Task RefreshSummaryAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var (snap, hardFaults) = await Task.Run(() => (MemoryAnalyzer.Snapshot(), MemoryAnalyzer.HardFaultsPerSecond()));
            Apply(snap, hardFaults);
        }
        catch (Exception ex) { SubText.Text = $"Não foi possível ler a memória: {ex.Message}"; }
        finally { _busy = false; }
    }

    private static void SetCol(ColumnDefinition c, ulong bytes) => c.Width = new GridLength((double)bytes, GridUnitType.Star);

    private void Apply(MemorySnapshot s, long? hardFaults)
    {
        double total = Math.Max(1, s.TotalPhysical);
        string Pct(ulong v) => $"{v * 100.0 / total:0}%";

        bool lists = s.ListsAvailable;
        ulong modified = s.Modified + s.ModifiedNoWrite;
        ulong freeSeg = lists ? s.FreeAndZero + s.Bad : s.Available;

        SetCol(ColInUse, s.InUse);
        SetCol(ColModified, lists ? modified : 0);
        SetCol(ColStandby, lists ? s.Standby : 0);
        SetCol(ColFree, freeSeg);

        BarTitle.Text = $"USO DA RAM · {MemoryAnalyzer.Size(s.TotalPhysical)}";
        LegInUse.Text = $"Em uso {MemoryAnalyzer.Size(s.InUse)} ({Pct(s.InUse)})";
        LegModified.Text = lists ? $"Modificada {MemoryAnalyzer.Size(modified)} ({Pct(modified)})" : "Modificada —";
        LegStandby.Text = lists ? $"Standby {MemoryAnalyzer.Size(s.Standby)} ({Pct(s.Standby)})" : "Standby —";
        LegFree.Text = (lists ? "Livre " : "Disponível ") + $"{MemoryAnalyzer.Size(freeSeg)} ({Pct(freeSeg)})";

        StTotal.Text = MemoryAnalyzer.Size(s.TotalPhysical);
        StTotalSub.Text = $"Disponível: {MemoryAnalyzer.Size(s.Available)} ({Pct(s.Available)}) · {s.Processes:N0} processos · {s.Threads:N0} threads · {s.Handles:N0} handles";
        StInUse.Text = MemoryAnalyzer.Size(s.InUse);
        StStandby.Text = lists ? MemoryAnalyzer.Size(s.Standby) : "—";
        StModified.Text = lists ? MemoryAnalyzer.Size(modified) : "—";
        StFree.Text = lists ? MemoryAnalyzer.Size(s.FreeAndZero) : "—";
        StFreeSub.Text = lists ? $"Zerada {MemoryAnalyzer.Size(s.Zeroed)} · livre {MemoryAnalyzer.Size(s.Free)}" : "Rode como administrador para ver as listas.";
        StCache.Text = MemoryAnalyzer.Size(s.SystemCache);
        StCommit.Text = MemoryAnalyzer.Size(s.CommitTotal);
        double commitPct = s.CommitLimit > 0 ? s.CommitTotal * 100.0 / s.CommitLimit : 0;
        StCommitSub.Text = $"Limite {MemoryAnalyzer.Size(s.CommitLimit)} ({commitPct:0}%) · pico {MemoryAnalyzer.Size(s.CommitPeak)}";
        StPools.Text = MemoryAnalyzer.Size(s.KernelPaged + s.KernelNonpaged);
        StPoolsSub.Text = $"Paginado {MemoryAnalyzer.Size(s.KernelPaged)} · não paginado {MemoryAnalyzer.Size(s.KernelNonpaged)}";

        var rows = new List<PriRow>();
        ulong max = lists ? s.StandbyByPriority.Max() : 0;
        for (int i = 7; i >= 0; i--)
        {
            ulong v = s.StandbyByPriority[i];
            rows.Add(new PriRow { Label = $"Prioridade {i}", Fraction = max > 0 ? (double)v / max : 0, Text = lists ? MemoryAnalyzer.Size(v) : "—" });
        }
        PriList.ItemsSource = rows;
        DiagList.ItemsSource = MemoryAnalyzer.Diagnose(s, hardFaults);
        SubText.Text = "Como o Windows está usando a RAM: em uso, em cache, esperando o disco ou livre. Estilo RAMMap.";
    }

    private async Task RefreshProcessesAsync()
    {
        try
        {
            var list = await Task.Run(() => MemoryAnalyzer.Processes());
            ProcGrid.ItemsSource = list;
            ApplyFilter();
            long sum = list.Sum(r => r.WorkingSet);
            ProcStatus.Text = $"{list.Count} processos · soma dos working sets: {MemoryAnalyzer.Size((ulong)Math.Max(0, sum))} " +
                              "(memória compartilhada é contada em cada processo, então a soma pode passar da RAM em uso).";
        }
        catch (Exception ex) { ProcStatus.Text = $"Não foi possível listar os processos: {ex.Message}"; }
    }

    private void ProcSearch_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (ProcGrid.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(ProcGrid.ItemsSource);
        if (view == null) return;
        string q = ProcSearch.Text.Trim();
        if (q.Length == 0) view.Filter = null;
        else view.Filter = o => o is ProcessMemRow r && (r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Pid.ToString() == q);
    }

    // ---------------- Ações ----------------

    private static string ActionName(MemoryAction a) => a switch
    {
        MemoryAction.WorkingSets => "Working sets",
        MemoryAction.SystemWorkingSet => "Working set do sistema",
        MemoryAction.ModifiedList => "Lista modificada",
        MemoryAction.StandbyList => "Standby",
        MemoryAction.StandbyLowPriority => "Standby prioridade 0",
        _ => a.ToString(),
    };

    private async Task RunActionsAsync(params MemoryAction[] actions)
    {
        if (_actionBusy) return;
        _actionBusy = true;
        ActionResult.Text = "Executando…";
        try
        {
            var results = await Task.Run(() =>
            {
                var list = new List<(MemoryAction Action, MemoryActionResult Result)>();
                foreach (var a in actions) list.Add((a, MemoryAnalyzer.Run(a)));
                return list;
            });

            var parts = new List<string>();
            foreach (var (action, r) in results)
            {
                string text = !r.Ok ? $"falhou ({r.Message})"
                    : r.FreedBytes > 8L * 1048576 ? $"+{MemoryAnalyzer.Size((ulong)r.FreedBytes)} disponíveis"
                    : "sem mudança relevante";
                parts.Add($"{ActionName(action)}: {text}");
            }
            ActionResult.Text = string.Join(" · ", parts);
        }
        catch (Exception ex) { ActionResult.Text = $"Erro: {ex.Message}"; }
        finally { _actionBusy = false; }

        await RefreshSummaryAsync();
        await RefreshProcessesAsync();
    }

    private async void BtnEmptyWs_Click(object sender, RoutedEventArgs e) => await RunActionsAsync(MemoryAction.WorkingSets);
    private async void BtnEmptySys_Click(object sender, RoutedEventArgs e) => await RunActionsAsync(MemoryAction.SystemWorkingSet);
    private async void BtnEmptyMod_Click(object sender, RoutedEventArgs e) => await RunActionsAsync(MemoryAction.ModifiedList);
    private async void BtnEmptyStandby_Click(object sender, RoutedEventArgs e) => await RunActionsAsync(MemoryAction.StandbyList);
    private async void BtnEmptyStandby0_Click(object sender, RoutedEventArgs e) => await RunActionsAsync(MemoryAction.StandbyLowPriority);

    private async void BtnEmptyAll_Click(object sender, RoutedEventArgs e) =>
        await RunActionsAsync(MemoryAction.WorkingSets, MemoryAction.SystemWorkingSet, MemoryAction.ModifiedList, MemoryAction.StandbyList);

    private async void BtnTrimSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ProcGrid.SelectedItem is not ProcessMemRow row) { ActionResult.Text = "Selecione um processo na tabela."; return; }
        bool ok = await Task.Run(() => MemoryAnalyzer.TrimProcess(row.Pid));
        ActionResult.Text = ok ? $"Working set de {row.Name} ({row.Pid}) esvaziado." : $"O Windows não deixou esvaziar {row.Name} ({row.Pid}).";
        await RefreshSummaryAsync();
        await RefreshProcessesAsync();
    }
}
