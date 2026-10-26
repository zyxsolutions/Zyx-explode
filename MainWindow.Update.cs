using System.Windows;
using System.Windows.Controls;

namespace ZyxExplode;

/// <summary>Atualização em um clique: confere a versão publicada no GitHub e instala sem sair do programa.</summary>
public partial class MainWindow
{
    private UpdateInfo? _update;
    private bool _updating;

    /// <summary>Ícone + texto (o botão da barra usa a fonte de ícones, então o texto precisa de um TextBlock próprio).</summary>
    private void SetUpdateLabel(string? text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock { Text = "\uE895", FontFamily = (System.Windows.Media.FontFamily)FindResource("Icons"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        if (text != null)
            panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        BtnUpdate.Content = panel;
        BtnUpdate.Width = double.NaN;
        BtnUpdate.Padding = new Thickness(text == null ? 14 : 12, 0, text == null ? 14 : 12, 0);
    }

    private async void InitUpdate()
    {
        await Task.Delay(4000); // não disputa com a abertura do programa
        await CheckUpdateAsync(silent: true);
    }

    private async Task CheckUpdateAsync(bool silent)
    {
        BtnUpdate.ToolTip = "Verificando atualizações…";
        try
        {
            _update = await AppUpdater.CheckAsync();
            if (_update.IsNewer)
            {
                SetUpdateLabel("ATUALIZAR PARA " + _update.Latest.ToString(3));
                BtnUpdate.ToolTip = $"Versão {_update.Latest.ToString(3)} disponível (você tem {AppUpdater.CurrentText}). Clique para atualizar.";
            }
            else
            {
                SetUpdateLabel(null);
                BtnUpdate.ToolTip = $"ZYX EXPLODE {AppUpdater.CurrentText} · você está na versão mais recente. Clique para verificar de novo.";
                if (!silent)
                    MessageBox.Show($"Você já está na versão mais recente ({AppUpdater.CurrentText}).", "ZYX EXPLODE", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            BtnUpdate.ToolTip = "Não foi possível verificar atualizações.";
            if (!silent)
                MessageBox.Show("Não foi possível verificar atualizações agora. Confira a internet e tente de novo.\n\n" + ex.Message,
                    "ZYX EXPLODE", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        if (_update is not { IsNewer: true } info)
        {
            await CheckUpdateAsync(silent: false);
            return;
        }

        var ask = MessageBox.Show(
            $"Atualizar o ZYX EXPLODE de {AppUpdater.CurrentText} para {info.Latest.ToString(3)}?\n\n" +
            "O programa baixa a versão nova, fecha, atualiza e abre sozinho. Suas configurações e históricos são mantidos.",
            "ZYX EXPLODE", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes) return;

        _updating = true;
        BtnUpdate.IsEnabled = false;
        var progress = new Progress<double>(p => SetUpdateLabel($"BAIXANDO {p:0}%"));
        string? error = await AppUpdater.InstallAsync(info, progress);
        if (error != null)
        {
            _updating = false;
            BtnUpdate.IsEnabled = true;
            SetUpdateLabel("ATUALIZAR PARA " + info.Latest.ToString(3));
            MessageBox.Show(error, "ZYX EXPLODE", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SetUpdateLabel("ATUALIZANDO…");
        await Task.Delay(1500);
        Application.Current.Shutdown();
    }
}
