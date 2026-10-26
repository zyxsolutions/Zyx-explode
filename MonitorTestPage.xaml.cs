using System.Windows;
using System.Windows.Controls;

namespace ZyxExplode;

/// <summary>ZYX DESEMPENHO · Teste de monitor: explica os testes e abre a janela em tela cheia.</summary>
public partial class MonitorTestPage : UserControl
{
    public MonitorTestPage() => InitializeComponent();

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var win = new MonitorTestWindow(Window.GetWindow(this));
        win.Show();
        win.Activate();
    }
}
