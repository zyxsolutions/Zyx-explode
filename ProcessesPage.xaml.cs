using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ZyxExplode;

/// <summary>ZYX DESEMPENHO · Processos: lista, prioridade, núcleos, prioridade de disco e regras salvas (estilo Process Lasso).</summary>
public partial class ProcessesPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _ruleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly List<CheckBox> _cores = new();
    private bool _busy, _ruleBusy;
    private int _loadedPid = -1;
    private ProcRow? _selected;

    public ProcessesPage()
    {
        InitializeComponent();
        for (int i = 0; i < Math.Min(Environment.ProcessorCount, 64); i++)
        {
            var cb = new CheckBox { Content = i.ToString(), Margin = new Thickness(0, 0, 14, 6), IsChecked = true, MinWidth = 36 };
            _cores.Add(cb);
            CorePanel.Children.Add(cb);
        }
        _timer.Tick += async (_, _) => { if (ChkAuto.IsChecked == true) await RefreshAsync(); };
        // As regras valem mesmo com a página fechada: confere programas novos a cada poucos segundos.
        _ruleTimer.Tick += async (_, _) =>
        {
            if (_ruleBusy) return;
            _ruleBusy = true;
            try { await Task.Run(() => ProcessManager.ApplyRules()); } catch { }
            finally { _ruleBusy = false; }
        };
        _ruleTimer.Start();
        ShowRules();
    }

    private async void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) { _timer.Start(); await RefreshAsync(); }
        else _timer.Stop();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void ProcSearch_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    // ---------------- Lista ----------------

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            int? keep = _selected?.Pid;
            var list = await Task.Run(() => ProcessManager.List());
            ProcGrid.ItemsSource = list;
            ApplyFilter();
            if (keep is int pid)
            {
                var again = list.FirstOrDefault(r => r.Pid == pid);
                if (again != null) { ProcGrid.SelectedItem = again; _selected = again; }
            }
            double cpu = list.Sum(r => r.Cpu);
            ProcStatus.Text = $"{list.Count} processos · CPU somada dos programas: {Math.Min(cpu, 100):0}%";
        }
        catch (Exception ex) { ProcStatus.Text = "Não foi possível listar os processos: " + ex.Message; }
        finally { _busy = false; }
    }

    private void ApplyFilter()
    {
        if (ProcGrid.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(ProcGrid.ItemsSource);
        string q = ProcSearch.Text.Trim();
        view.Filter = string.IsNullOrEmpty(q) ? null : o => o is ProcRow r && r.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- Editor ----------------

    private void ProcGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcGrid.SelectedItem is not ProcRow row) return;
        _selected = row;
        if (row.Pid == _loadedPid) return; // atualização automática da lista: não sobrescreve o que o usuário está mexendo
        _loadedPid = row.Pid;

        EditCard.IsEnabled = true;
        EditCard.Opacity = 1;
        EditTitle.Text = $"AJUSTAR · {row.Name.ToUpperInvariant()} (PID {row.Pid})";
        EditResult.Text = ProcessManager.IsProtected(row.Name) ? "Processo essencial do Windows: o Windows pode recusar mudanças." : "";

        foreach (ComboBoxItem it in CmbPriority.Items)
            if ((string)it.Content == row.Priority) CmbPriority.SelectedItem = it;
        if (CmbPriority.SelectedItem == null) CmbPriority.SelectedIndex = 2;

        for (int i = 0; i < _cores.Count; i++)
            _cores[i].IsChecked = row.AffinityMask == 0 || (row.AffinityMask & (1L << i)) != 0;

        var rule = ProcessManager.LoadRules().FirstOrDefault(r => r.Name.Equals(row.Name, StringComparison.OrdinalIgnoreCase));
        ChkRule.IsChecked = rule != null;
        ChkLowIo.IsChecked = rule?.LowIo ?? false;
    }

    private long CoreMask()
    {
        long m = 0;
        for (int i = 0; i < _cores.Count; i++) if (_cores[i].IsChecked == true) m |= 1L << i;
        return m;
    }

    private void SetCores(Func<int, bool> on)
    {
        for (int i = 0; i < _cores.Count; i++) _cores[i].IsChecked = on(i);
    }

    private void BtnCoresAll_Click(object sender, RoutedEventArgs e) => SetCores(_ => true);
    private void BtnCoresFirst_Click(object sender, RoutedEventArgs e) => SetCores(i => i < (_cores.Count + 1) / 2);
    private void BtnCoresSecond_Click(object sender, RoutedEventArgs e) => SetCores(i => i >= _cores.Count / 2);

    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not ProcRow row) return;
        string key = (CmbPriority.SelectedItem as ComboBoxItem)?.Tag as string ?? "normal";
        long mask = CoreMask();
        bool lowIo = ChkLowIo.IsChecked == true;
        bool remember = ChkRule.IsChecked == true;
        long all = _cores.Count >= 63 ? -1L : (1L << _cores.Count) - 1;

        var msgs = new List<string>();
        int pid = row.Pid;
        var res = await Task.Run(() =>
        {
            var list = new List<ProcActionResult>();
            if (ProcessManager.PriorityFromKey(key) is ProcessPriorityClass c) list.Add(ProcessManager.SetPriority(pid, c));
            list.Add(ProcessManager.SetAffinity(pid, mask));
            list.Add(ProcessManager.SetLowIo(pid, lowIo));
            return list;
        });
        foreach (var r in res) if (!r.Ok) msgs.Add(r.Message);

        if (remember)
        {
            ProcessManager.SaveRule(new ProcRule
            {
                Name = row.Name, Priority = key,
                AffinityMask = (mask & all) == all ? null : mask,   // "todos" = não mexe
                LowIo = lowIo,
            });
            ShowRules();
        }
        else if (ProcessManager.LoadRules().Any(r => r.Name.Equals(row.Name, StringComparison.OrdinalIgnoreCase)))
        {
            ProcessManager.RemoveRule(row.Name);
            ShowRules();
        }

        EditResult.Text = msgs.Count == 0
            ? "Ajustes aplicados." + (remember ? " Regra salva." : "")
            : "Alguns ajustes não foram aplicados: " + string.Join("; ", msgs.Distinct()) + ".";
        _loadedPid = -1; // recarrega o editor com os valores reais depois da atualização
        await RefreshAsync();
    }

    private async void BtnEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not ProcRow row) return;
        if (MessageBox.Show($"Encerrar {row.Name} (PID {row.Pid})?\n\nO que não foi salvo nele será perdido.",
                "ZYX EXPLODE", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        int pid = row.Pid;
        var r = await Task.Run(() => ProcessManager.EndProcess(pid, false));
        EditResult.Text = r.Ok ? "Programa encerrado." : "Não foi possível encerrar: " + r.Message + ".";
        _selected = null;
        _loadedPid = -1;
        await RefreshAsync();
    }

    // ---------------- Regras ----------------

    private void ShowRules()
    {
        var rules = ProcessManager.LoadRules();
        RulesGrid.ItemsSource = rules;
        bool any = rules.Count > 0;
        RulesGrid.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        BtnRemoveRule.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        RulesEmpty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not ProcRule rule) return;
        ProcessManager.RemoveRule(rule.Name);
        ShowRules();
        EditResult.Text = $"Regra de {rule.Name} removida. Os programas já abertos mantêm o ajuste até fecharem.";
    }
}
