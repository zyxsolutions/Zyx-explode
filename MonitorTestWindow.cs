using System.Globalization;
using System.Management;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ZyxExplode;

/// <summary>Janela em tela cheia com testes de monitor: pixels mortos, bandas, movimento, padrões de pixel, geometria e quadros.</summary>
public sealed class MonitorTestWindow : Window
{
    public MonitorTestWindow(Window? owner)
    {
        Title = "ZYX EXPLODE · Teste de monitor";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Black;
        Topmost = true;
        Cursor = Cursors.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        if (owner != null) { Left = owner.Left; Top = owner.Top; } // abre no monitor onde o programa está
        WindowState = WindowState.Maximized;

        var surface = new TestSurface();
        Content = surface;
        surface.CloseRequested += (_, _) => Close();
        KeyDown += (_, e) => { surface.HandleKey(e.Key); e.Handled = true; };
        MouseLeftButtonDown += (_, _) => surface.HandleKey(Key.Right);
        MouseRightButtonDown += (_, _) => surface.HandleKey(Key.Left);
    }
}

/// <summary>Superfície que desenha o teste atual. Tudo é desenhado aqui; o HUD (caixa de ajuda) some depois de alguns segundos.</summary>
internal sealed class TestSurface : FrameworkElement
{
    public event EventHandler? CloseRequested;

    private enum Mode { Solid, Gradient, Motion, Pixels, Grid, Frames }

    private static readonly string[] ModeNames =
    {
        "Cores sólidas (pixels mortos, vazamento de luz)", "Gradientes (bandas de cor)", "Movimento (rastro e fantasma)",
        "Padrões de pixel (nitidez)", "Grade e geometria", "Taxa de atualização e quadros pulados",
    };

    private static readonly (string Name, Color Color)[] SolidColors =
    {
        ("Preto", Colors.Black), ("Branco", Colors.White), ("Vermelho", Colors.Red), ("Verde", Colors.Lime), ("Azul", Colors.Blue),
        ("Cinza 50%", Color.FromRgb(128, 128, 128)), ("Amarelo", Colors.Yellow), ("Ciano", Colors.Cyan), ("Magenta", Colors.Magenta),
    };

    private static readonly double[] Speeds = { 480, 960, 1440, 1920 };
    private const double BoxSize = 160;

    private Mode _mode = Mode.Solid;
    private int _variant, _speedIdx = 1;
    private double _boxX;
    private TimeSpan _lastRender = TimeSpan.MinValue;
    private readonly List<double> _dts = new();           // intervalos entre quadros, em ms (últimos 240)
    private readonly Queue<TimeSpan> _skips = new();      // quando houve quadros pulados (últimos 10 s)
    private DateTime _hudUntil = DateTime.Now.AddSeconds(8);
    private bool _hudPinned, _hudWasVisible = true;
    private int _hz;
    private string _resolution = "";

    public TestSurface()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        Loaded += async (_, _) =>
        {
            CompositionTarget.Rendering += OnRendering;
            var (hz, res) = await Task.Run(() => ReadDisplay());
            _hz = hz;
            _resolution = res;
            InvalidateVisual();
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    private static (int Hz, string Resolution) ReadDisplay()
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController WHERE CurrentRefreshRate > 0");
            foreach (ManagementObject mo in s.Get())
                return (Convert.ToInt32(mo["CurrentRefreshRate"]), $"{mo["CurrentHorizontalResolution"]}x{mo["CurrentVerticalResolution"]}");
        }
        catch { }
        return (0, "");
    }

    // ---------------- Entrada ----------------

    private int VariantCount => _mode switch
    {
        Mode.Solid => SolidColors.Length, Mode.Gradient => 4, Mode.Motion => 3, Mode.Pixels => 3, _ => 1,
    };

    public void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Escape: CloseRequested?.Invoke(this, EventArgs.Empty); return;
            case Key.Tab: SetMode((Mode)(((int)_mode + 1) % ModeNames.Length)); break;
            case Key.D1 or Key.NumPad1: SetMode(Mode.Solid); break;
            case Key.D2 or Key.NumPad2: SetMode(Mode.Gradient); break;
            case Key.D3 or Key.NumPad3: SetMode(Mode.Motion); break;
            case Key.D4 or Key.NumPad4: SetMode(Mode.Pixels); break;
            case Key.D5 or Key.NumPad5: SetMode(Mode.Grid); break;
            case Key.D6 or Key.NumPad6: SetMode(Mode.Frames); break;
            case Key.Right or Key.Space: _variant = (_variant + 1) % VariantCount; break;
            case Key.Left: _variant = (_variant + VariantCount - 1) % VariantCount; break;
            case Key.Up: _speedIdx = Math.Min(Speeds.Length - 1, _speedIdx + 1); break;
            case Key.Down: _speedIdx = Math.Max(0, _speedIdx - 1); break;
            case Key.H: _hudPinned = !_hudPinned; break;
        }
        _hudUntil = DateTime.Now.AddSeconds(4);
        InvalidateVisual();
    }

    private void SetMode(Mode m)
    {
        _mode = m;
        _variant = 0;
        _boxX = -BoxSize;
    }

    private bool HudVisible => _hudPinned || DateTime.Now < _hudUntil || _mode == Mode.Frames;

    // ---------------- Quadros ----------------

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastRender) return; // o WPF pode avisar mais de uma vez pelo mesmo quadro
        if (_lastRender == TimeSpan.MinValue) { _lastRender = now; return; }

        double ms = (now - _lastRender).TotalMilliseconds;
        _lastRender = now;

        _dts.Add(ms);
        if (_dts.Count > 240) _dts.RemoveAt(0);
        if (_dts.Count >= 30)
        {
            double median = Median(_dts.Skip(Math.Max(0, _dts.Count - 120)));
            if (ms > median * 1.5) _skips.Enqueue(now);
        }
        while (_skips.Count > 0 && (now - _skips.Peek()).TotalSeconds > 10) _skips.Dequeue();

        _boxX += Speeds[_speedIdx] * ms / 1000.0;
        if (_boxX > ActualWidth) _boxX = -BoxSize;

        bool animated = _mode is Mode.Motion or Mode.Frames;
        bool hud = HudVisible;
        if (animated || hud != _hudWasVisible) InvalidateVisual();
        _hudWasVisible = hud;
    }

    private static double Median(IEnumerable<double> values)
    {
        var a = values.OrderBy(v => v).ToArray();
        return a.Length == 0 ? 0 : a[a.Length / 2];
    }

    // ---------------- Desenho ----------------

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var dpi = VisualTreeHelper.GetDpi(this);
        switch (_mode)
        {
            case Mode.Solid: dc.DrawRectangle(new SolidColorBrush(SolidColors[_variant].Color), null, new Rect(0, 0, w, h)); break;
            case Mode.Gradient: DrawGradient(dc, w, h); break;
            case Mode.Motion: DrawMotion(dc, w, h); break;
            case Mode.Pixels: DrawPixels(dc, w, h, dpi); break;
            case Mode.Grid: DrawGrid(dc, w, h); break;
            case Mode.Frames: DrawFrames(dc, w, h, dpi); break;
        }
        if (HudVisible) DrawHud(dc, dpi.PixelsPerDip);
    }

    private void DrawGradient(DrawingContext dc, double w, double h)
    {
        Color end = _variant switch { 1 => Colors.Red, 2 => Colors.Lime, 3 => Colors.Blue, _ => Colors.White };
        dc.DrawRectangle(new LinearGradientBrush(Colors.Black, end, 0), null, new Rect(0, 0, w, h * 0.6));
        const int steps = 32; // degraus visíveis embaixo, para comparar com a rampa contínua
        double sw = w / steps;
        for (int i = 0; i < steps; i++)
        {
            double t = i / (steps - 1.0);
            var c = Color.FromRgb((byte)(end.R * t), (byte)(end.G * t), (byte)(end.B * t));
            dc.DrawRectangle(new SolidColorBrush(c), null, new Rect(i * sw, h * 0.6, sw + 1, h * 0.4));
        }
    }

    private void DrawMotion(DrawingContext dc, double w, double h)
    {
        Color bg = _variant switch { 1 => Colors.White, 2 => Color.FromRgb(128, 128, 128), _ => Color.FromRgb(32, 32, 32) };
        dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, w, h));

        double y = (h - BoxSize) / 2;
        // Quadrado com listras finas: borrão e fantasma aparecem como listras apagadas ou um rastro atrás dele.
        dc.DrawRectangle(Brushes.White, null, new Rect(_boxX, y, BoxSize, BoxSize));
        for (double x = 0; x < BoxSize; x += 8)
            dc.DrawRectangle(Brushes.Black, null, new Rect(_boxX + x, y, 4, BoxSize));
        dc.DrawRectangle(null, new Pen(Brushes.Red, 2), new Rect(_boxX, y, BoxSize, BoxSize));
    }

    private static Drawing PixelPattern(int variant, double px, double py)
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 2 * px, 2 * py))));
        switch (variant)
        {
            case 0: g.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, px, 2 * py)))); break;
            case 1: g.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, 2 * px, py)))); break;
            default:
                g.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, px, py))));
                g.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(px, py, px, py))));
                break;
        }
        return g;
    }

    private void DrawPixels(DrawingContext dc, double w, double h, DpiScale dpi)
    {
        double px = 1.0 / dpi.DpiScaleX, py = 1.0 / dpi.DpiScaleY; // 1 pixel físico, em unidades do WPF
        var tile = new Rect(0, 0, 2 * px, 2 * py);
        var brush = new DrawingBrush(PixelPattern(_variant, px, py))
        {
            TileMode = TileMode.Tile, Stretch = Stretch.Fill,
            Viewbox = tile, ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = tile, ViewportUnits = BrushMappingMode.Absolute,
        };
        dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
    }

    private void DrawGrid(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 16, 16)), null, new Rect(0, 0, w, h));
        var thin = new Pen(new SolidColorBrush(Color.FromRgb(70, 70, 70)), 1);
        var bold = new Pen(Brushes.White, 2);
        double cx = w / 2, cy = h / 2;

        for (double x = cx % 100; x < w; x += 100) dc.DrawLine(thin, new Point(x, 0), new Point(x, h));
        for (double y = cy % 100; y < h; y += 100) dc.DrawLine(thin, new Point(0, y), new Point(w, y));
        dc.DrawLine(bold, new Point(cx, 0), new Point(cx, h));
        dc.DrawLine(bold, new Point(0, cy), new Point(w, cy));
        dc.DrawLine(thin, new Point(0, 0), new Point(w, h));
        dc.DrawLine(thin, new Point(w, 0), new Point(0, h));

        double r = Math.Min(w, h) * 0.4; // se o círculo parecer oval, a proporção da tela está errada
        dc.DrawEllipse(null, bold, new Point(cx, cy), r, r);
        dc.DrawEllipse(null, thin, new Point(cx, cy), r / 2, r / 2);

        // Barras de cor no alto e marcas nos 4 cantos (mostram se a imagem está cortada).
        Color[] bars = { Colors.White, Colors.Yellow, Colors.Cyan, Colors.Lime, Colors.Magenta, Colors.Red, Colors.Blue };
        double bw = w / bars.Length;
        for (int i = 0; i < bars.Length; i++)
            dc.DrawRectangle(new SolidColorBrush(bars[i]), null, new Rect(i * bw, 0, bw + 1, h * 0.08));
        const double m = 20;
        foreach (var p in new[] { new Point(0, h * 0.08), new Point(w - m, h * 0.08), new Point(0, h - m), new Point(w - m, h - m) })
            dc.DrawRectangle(Brushes.White, null, new Rect(p.X, p.Y, m, m));
    }

    private void DrawFrames(DrawingContext dc, double w, double h, DpiScale dpi)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12, 12, 12)), null, new Rect(0, 0, w, h));
        double ppd = dpi.PixelsPerDip;

        // Quadrado em movimento: mantém a janela pedindo quadros novos o tempo todo.
        double y = h * 0.34;
        dc.DrawRectangle(Brushes.White, null, new Rect(_boxX, y, 90, 90));

        double median = Median(_dts);
        double avg = _dts.Count > 0 ? _dts.Average() : 0;
        double worst = _dts.Count > 0 ? _dts.Max() : 0;
        double fps = avg > 0 ? 1000.0 / avg : 0;
        var mono = new Typeface("Consolas");

        var lines = new List<(string Text, double Size, Brush Brush)>
        {
            (_hz > 0 ? $"Taxa configurada no Windows: {_hz} Hz" + (_resolution.Length > 0 ? $" · {_resolution}" : "") : "Taxa configurada no Windows: não foi possível ler", 22, Brushes.White),
            ($"Quadros desta janela: {fps:0.0} por segundo (intervalo médio {avg:0.00} ms)", 22, Brushes.White),
            ($"Mediana {median:0.00} ms · pior intervalo {worst:0.00} ms (últimos {_dts.Count} quadros)", 16, Brushes.LightGray),
            ($"Quadros pulados nos últimos 10 s: {_skips.Count}", 22, _skips.Count > 0 ? Brushes.OrangeRed : Brushes.LimeGreen),
            ("Compare os dois primeiros números. A janela pode ser limitada pelo WPF; confirme a taxa real em Configurações › Tela.", 13, Brushes.Gray),
        };
        double ty = h * 0.50;
        foreach (var (text, size, brush) in lines)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, mono, size, brush, ppd);
            dc.DrawText(ft, new Point(40, ty));
            ty += size * 1.6;
        }

        // Gráfico: uma barra por quadro; as vermelhas passaram de 1,5x a mediana (quadro pulado).
        double gx = 40, gw = w - 80, gh = h * 0.10, gy = h - gh - 40;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(28, 28, 28)), null, new Rect(gx, gy - gh, gw, gh * 2));
        double scale = median > 0 ? gh / (median * 3) : 1;
        double bar = gw / 240.0;
        for (int i = 0; i < _dts.Count; i++)
        {
            double bh = Math.Min(gh * 2, _dts[i] * scale);
            bool skipped = median > 0 && _dts[i] > median * 1.5;
            dc.DrawRectangle(skipped ? Brushes.OrangeRed : Brushes.White, null, new Rect(gx + i * bar, gy + gh - bh, Math.Max(1, bar - 1), bh));
        }
    }

    private void DrawHud(DrawingContext dc, double ppd)
    {
        var lines = new List<string>
        {
            $"TESTE {(int)_mode + 1} de {ModeNames.Length} · {ModeNames[(int)_mode]}",
            VariantText(),
            "",
            "← → ou clique: muda a variação · Tab ou 1 a 6: troca de teste",
        };
        if (_mode == Mode.Motion) lines.Add("↑ ↓: velocidade · siga o quadrado com os olhos: bordas borradas ou sombra atrás = resposta lenta");
        if (_mode == Mode.Pixels) lines.Add("Use a escala do Windows em 100% para ver os pixels exatos. Linhas cinzentas ou tremidas = problema de sinal ou escala.");
        if (_mode == Mode.Solid) lines.Add("Procure pontos que não mudam de cor (pixels mortos ou presos) e manchas claras no preto (vazamento de luz).");
        lines.Add("H: fixa ou esconde esta caixa · Esc: sair");

        var typeface = new Typeface("Segoe UI");
        double width = 0, height = 0;
        var texts = new List<FormattedText>();
        for (int i = 0; i < lines.Count; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, i == 0 ? 16 : 13, Brushes.White, ppd);
            texts.Add(ft);
            width = Math.Max(width, ft.Width);
            height += ft.Height + 4;
        }
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), new Pen(new SolidColorBrush(Color.FromRgb(90, 90, 90)), 1),
                                new Rect(16, 16, width + 32, height + 24), 8, 8);
        double y = 28;
        foreach (var ft in texts)
        {
            dc.DrawText(ft, new Point(32, y));
            y += ft.Height + 4;
        }
    }

    private string VariantText() => _mode switch
    {
        Mode.Solid => $"Cor: {SolidColors[_variant].Name} ({_variant + 1}/{SolidColors.Length})",
        Mode.Gradient => $"Rampa: {new[] { "cinza", "vermelho", "verde", "azul" }[_variant]} ({_variant + 1}/4) · em cima contínua, embaixo em 32 degraus",
        Mode.Motion => $"Fundo: {new[] { "escuro", "branco", "cinza" }[_variant]} ({_variant + 1}/3) · velocidade {Speeds[_speedIdx]:0} px/s",
        Mode.Pixels => $"Padrão: {new[] { "colunas de 1 pixel", "linhas de 1 pixel", "xadrez de 1 pixel" }[_variant]} ({_variant + 1}/3)",
        Mode.Grid => "Círculos devem ser redondos, linhas retas e as marcas dos 4 cantos visíveis",
        _ => "Observe o gráfico: barras vermelhas são quadros pulados",
    };
}
