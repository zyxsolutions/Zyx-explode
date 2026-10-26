using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ZyxExplode;

/// <summary>ZYX FERRAMENTAS · Manutenção: reparo do Windows, rede, energia e limpeza, com saída ao vivo.</summary>
public partial class MaintenancePage : UserControl
{
    private readonly List<MaintTask> _tasks = MaintenanceRunner.Catalog();
    private readonly ObservableCollection<string> _log = new();
    private CancellationTokenSource? _cts;
    private bool _running;

    public MaintenancePage()
    {
        InitializeComponent();
        foreach (var t in _tasks) t.Selected = t.PreSelect;

        var view = CollectionViewSource.GetDefaultView(_tasks);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MaintTask.Group)));
        TaskList.ItemsSource = view;
        MaintLog.ItemsSource = _log;
        MaintStatus.Text = "Marque o que quer fazer e clique em EXECUTAR SELECIONADAS.";
    }

    private void BtnQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        foreach (var t in _tasks) t.Selected = !t.Slow && !t.NeedsReboot && !t.CutsNetwork && !t.Destructive;
    }

    private void BtnNone_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        foreach (var t in _tasks) t.Selected = false;
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => _log.Clear();

    private void AddLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _log.Add(text);
        if (_log.Count > 2000) _log.RemoveAt(0);
        MaintLog.ScrollIntoView(_log[^1]);
    }

    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (_running)
        {
            _cts?.Cancel();
            BtnRun.IsEnabled = false;
            MaintStatus.Text = "Cancelando…";
            return;
        }

        var sel = _tasks.Where(t => t.Selected).ToList();
        if (sel.Count == 0) { MaintStatus.Text = "Nenhuma tarefa marcada."; return; }

        var warnings = new List<string>();
        if (sel.Any(t => t.Slow)) warnings.Add("• Algumas tarefas demoram (de alguns minutos a mais de meia hora). Deixe o PC na tomada.");
        if (sel.Any(t => t.CutsNetwork)) warnings.Add("• A internet vai cair por alguns segundos.");
        if (sel.Any(t => t.NeedsReboot)) warnings.Add("• Será preciso reiniciar o PC depois para valer.");
        if (sel.Any(t => t.Destructive)) warnings.Add("• A lixeira será esvaziada de vez: não dá para recuperar depois.");
        if (warnings.Count > 0)
        {
            var r = MessageBox.Show($"Executar {sel.Count} tarefa(s)?\n\n" + string.Join("\n", warnings), "ZYX MANUTENÇÃO",
                                    MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        _running = true;
        _cts = new CancellationTokenSource();
        BtnRun.Content = "CANCELAR";
        foreach (var t in sel) t.Status = "Na fila.";

        int ok = 0, failed = 0;
        bool cancelled = false;
        for (int i = 0; i < sel.Count; i++)
        {
            var t = sel[i];
            if (_cts.IsCancellationRequested) { t.Status = "Não executada (cancelado)."; cancelled = true; continue; }

            MaintStatus.Text = $"Executando {i + 1} de {sel.Count}: {t.Title}";
            MaintProgress.Text = "";
            t.Status = "Executando…";
            AddLog($"▶ {t.Title}");
            AddLog($"  {t.CommandText}");
            try
            {
                var (success, message) = await MaintenanceRunner.RunAsync(t, OnLine, _cts.Token);
                t.Status = message;
                AddLog($"  {(success ? "✓" : "✗")} {message}");
                if (success) ok++; else failed++;
            }
            catch (OperationCanceledException)
            {
                t.Status = "Cancelada.";
                AddLog("  ✗ Cancelada.");
                cancelled = true;
            }
            catch (Exception ex)
            {
                t.Status = $"Falhou: {ex.Message}";
                AddLog($"  ✗ {ex.Message}");
                failed++;
            }
        }

        _running = false;
        _cts.Dispose();
        _cts = null;
        BtnRun.Content = "EXECUTAR SELECIONADAS";
        BtnRun.IsEnabled = true;
        MaintProgress.Text = "";
        MaintStatus.Text = $"{ok} concluída(s)" + (failed > 0 ? $", {failed} com problema" : "") + (cancelled ? " · interrompido" : "") + ".";
    }

    /// <summary>Linhas de progresso (barra de %) só atualizam a linha de cima; o resto vai para o registro.</summary>
    private void OnLine(string text, bool progress)
    {
        if (progress) MaintProgress.Text = text.Trim();
        else AddLog("  " + text);
    }
}
