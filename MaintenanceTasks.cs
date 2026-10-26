using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ZyxExplode;

/// <summary>Uma tarefa de manutenção: um comando do Windows (mostrado antes de rodar) ou uma função interna.</summary>
public sealed class MaintTask : INotifyPropertyChanged
{
    public string Group { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Texto mostrado ao usuário: o comando exato que será executado.</summary>
    public string CommandText { get; init; } = "";
    /// <summary>Programa em System32 (vazio = tarefa interna do app).</summary>
    public string File { get; init; } = "";
    public string Args { get; init; } = "";
    /// <summary>O SFC escreve em UTF-16 quando a saída é redirecionada; os demais usam a página de código do console.</summary>
    public bool Utf16 { get; init; }
    public bool Slow { get; init; }
    public bool NeedsReboot { get; init; }
    public bool CutsNetwork { get; init; }
    public bool Destructive { get; init; }
    public bool PreSelect { get; init; }
    public Func<Action<string>, CancellationToken, Task<(bool Ok, string Message)>>? Builtin { get; init; }

    public string Badge => Slow ? "DEMORA" : NeedsReboot ? "PEDE REINÍCIO" : CutsNetwork ? "DERRUBA A REDE" : Destructive ? "APAGA DEFINITIVO" : "";

    private bool _selected;
    private string _status = "";
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}

/// <summary>Catálogo e execução das tarefas de manutenção (SFC, DISM, DNS, plano de energia, limpeza...).</summary>
public static class MaintenanceRunner
{
    private static string SystemDrive => Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";

    public static List<MaintTask> Catalog()
    {
        const string repair = "REPARO DO WINDOWS", net = "REDE", power = "ENERGIA", clean = "LIMPEZA";
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string batteryFile = Path.Combine(docs, "ZYX-relatorio-bateria.html");

        return new List<MaintTask>
        {
            new() { Group = repair, Title = "Verificar arquivos do sistema (SFC)",
                    Description = "Procura arquivos protegidos do Windows corrompidos e repara os que achar. Se disser que não conseguiu reparar, rode o reparo da imagem (DISM) e tente de novo.",
                    CommandText = "sfc /scannow", File = "sfc.exe", Args = "/scannow", Utf16 = true, Slow = true },
            new() { Group = repair, Title = "Checar a integridade da imagem do Windows (rápido)",
                    Description = "Só informa se o Windows já marcou a imagem como danificada. Leva segundos e não altera nada.",
                    CommandText = "DISM /Online /Cleanup-Image /CheckHealth", File = "dism.exe", Args = "/Online /Cleanup-Image /CheckHealth", PreSelect = true },
            new() { Group = repair, Title = "Reparar a imagem do Windows (DISM)",
                    Description = "Troca componentes danificados do Windows por cópias boas, baixadas do Windows Update. Precisa de internet.",
                    CommandText = "DISM /Online /Cleanup-Image /RestoreHealth", File = "dism.exe", Args = "/Online /Cleanup-Image /RestoreHealth", Slow = true },
            new() { Group = repair, Title = "Limpar componentes antigos do Windows (WinSxS)",
                    Description = "Remove versões antigas de componentes que ficam depois das atualizações e libera espaço no disco.",
                    CommandText = "DISM /Online /Cleanup-Image /StartComponentCleanup", File = "dism.exe", Args = "/Online /Cleanup-Image /StartComponentCleanup", Slow = true },
            new() { Group = repair, Title = $"Verificar o disco {SystemDrive} (sem reiniciar)",
                    Description = "Busca erros no sistema de arquivos com o Windows em uso. Só lê; para corrigir, rode o chkdsk com /f pelo prompt (pede reinício).",
                    CommandText = $"chkdsk {SystemDrive} /scan", File = "chkdsk.exe", Args = $"{SystemDrive} /scan", Slow = true },

            new() { Group = net, Title = "Limpar o cache de DNS",
                    Description = "Esquece os endereços guardados. Resolve sites que não abrem depois de trocar de provedor ou de DNS.",
                    CommandText = "ipconfig /flushdns", File = "ipconfig.exe", Args = "/flushdns", PreSelect = true },
            new() { Group = net, Title = "Renovar o endereço IP",
                    Description = "Libera e pede um IP novo ao roteador. A internet cai por alguns segundos.",
                    CommandText = "ipconfig /release && ipconfig /renew", File = "cmd.exe", Args = "/c ipconfig /release && ipconfig /renew", CutsNetwork = true },
            new() { Group = net, Title = "Redefinir o Winsock",
                    Description = "Refaz o catálogo de rede do Windows. Corrige conexões que param de funcionar por causa de programas que mexem na rede. Pede reinício.",
                    CommandText = "netsh winsock reset", File = "netsh.exe", Args = "winsock reset", NeedsReboot = true },
            new() { Group = net, Title = "Redefinir a pilha TCP/IP",
                    Description = "Volta as configurações de TCP/IP ao padrão. Pede reinício.",
                    CommandText = "netsh int ip reset", File = "netsh.exe", Args = "int ip reset", NeedsReboot = true },

            new() { Group = power, Title = "Ativar o plano Alto desempenho",
                    Description = "O PC deixa de reduzir o clock para economizar energia. Em notebook, gasta mais bateria. Em alguns PCs o plano não existe.",
                    CommandText = "powercfg /setactive SCHEME_MIN", File = "powercfg.exe", Args = "/setactive SCHEME_MIN" },
            new() { Group = power, Title = "Voltar ao plano Equilibrado",
                    Description = "Plano padrão do Windows.",
                    CommandText = "powercfg /setactive SCHEME_BALANCED", File = "powercfg.exe", Args = "/setactive SCHEME_BALANCED" },
            new() { Group = power, Title = "Gerar o relatório de bateria",
                    Description = $"Cria um HTML com a capacidade de projeto e a atual da bateria. Só vale para notebook. Salvo em Documentos.",
                    CommandText = $"powercfg /batteryreport /output \"{batteryFile}\"", File = "powercfg.exe", Args = $"/batteryreport /output \"{batteryFile}\"" },

            new() { Group = clean, Title = "Apagar arquivos temporários antigos",
                    Description = "Remove o que tem mais de 1 dia nas pastas Temp do usuário e do Windows. O que estiver em uso é mantido.",
                    CommandText = "(interno) apaga arquivos com mais de 1 dia em %TEMP% e %WINDIR%\\Temp", Builtin = CleanTempAsync, PreSelect = true },
            new() { Group = clean, Title = "Esvaziar a lixeira",
                    Description = "Apaga definitivamente tudo que está na lixeira de todas as unidades.",
                    CommandText = "(interno) SHEmptyRecycleBin", Builtin = EmptyRecycleBinAsync, Destructive = true },
            new() { Group = clean, Title = "Abrir a Limpeza de Disco do Windows",
                    Description = "Abre a ferramenta clássica para você escolher o que apagar (inclusive arquivos de atualização antigos).",
                    CommandText = $"cleanmgr /d {SystemDrive.TrimEnd(':')}", File = "cleanmgr.exe", Args = $"/d {SystemDrive.TrimEnd(':')}" },
        };
    }

    // ---------------- Execução ----------------

    /// <summary>Roda uma tarefa. <paramref name="onLine"/> recebe (texto, éProgresso): progresso é a linha que o programa reescreve (barra de %).</summary>
    public static async Task<(bool Ok, string Message)> RunAsync(MaintTask t, Action<string, bool> onLine, CancellationToken ct)
    {
        if (t.Builtin != null) return await t.Builtin(s => onLine(s, false), ct);

        var enc = t.Utf16 ? Encoding.Unicode : OemEncoding();
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, t.File), t.Args)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = enc, StandardErrorEncoding = enc,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o programa.");
        using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });

        await Task.WhenAll(Pump(p.StandardOutput, onLine), Pump(p.StandardError, onLine), p.WaitForExitAsync());
        ct.ThrowIfCancellationRequested();

        int code = p.ExitCode;
        if (code == 0) return (true, "Concluído.");
        if (code == 3010) return (true, "Concluído. Reinicie o PC para terminar.");
        return (false, $"Terminou com código {code}. Veja o registro abaixo.");
    }

    private static Encoding OemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch { return Encoding.UTF8; }
    }

    /// <summary>
    /// Lê a saída aos poucos. Linhas terminadas em \n entram no registro; as terminadas só em \r (barras de progresso
    /// do DISM e do SFC) vão como "progresso", para a tela mostrar uma linha só em vez de centenas.
    /// </summary>
    private static async Task Pump(StreamReader reader, Action<string, bool> onLine)
    {
        var sb = new StringBuilder();
        string? cr = null; // texto terminado em \r esperando saber se vem \n depois
        var buf = new char[512];
        int n;
        while ((n = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c == '\r')
                {
                    if (sb.Length > 0)
                    {
                        if (cr != null) onLine(cr, true);
                        cr = sb.ToString();
                        sb.Clear();
                    }
                }
                else if (c == '\n')
                {
                    if (sb.Length > 0)
                    {
                        if (cr != null) onLine(cr, true);
                        onLine(sb.ToString().Trim(), false);
                        cr = null;
                        sb.Clear();
                    }
                    else if (cr != null) { onLine(cr.Trim(), false); cr = null; }
                }
                else if (c != '\0') sb.Append(c);
            }
        }
        if (cr != null) onLine(cr.Trim(), false);
        if (sb.Length > 0) onLine(sb.ToString().Trim(), false);
    }

    // ---------------- Tarefas internas ----------------

    private sealed class SweepState { public long Freed; public int Files, Skipped; }

    private static Task<(bool Ok, string Message)> CleanTempAsync(Action<string> log, CancellationToken ct) => Task.Run(() =>
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var state = new SweepState();
        var dirs = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        };
        foreach (var d in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(d)) continue;
            log($"Limpando {d}");
            Sweep(d, cutoff, state, ct);
        }
        ct.ThrowIfCancellationRequested();
        return (true, $"{state.Files:N0} arquivos removidos ({MemoryAnalyzer.Size((ulong)state.Freed)}); {state.Skipped:N0} em uso foram mantidos.");
    }, ct);

    private static void Sweep(string dir, DateTime cutoff, SweepState st, CancellationToken ct)
    {
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(dir).ToList(); } catch { return; }
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fi = new FileInfo(f);
                if (fi.LastWriteTimeUtc > cutoff) continue;
                long len = fi.Length;
                fi.Delete();
                st.Freed += len;
                st.Files++;
            }
            catch { st.Skipped++; }
        }

        IEnumerable<string> subs;
        try { subs = Directory.EnumerateDirectories(dir).ToList(); } catch { return; }
        foreach (var s in subs)
        {
            try
            {
                if ((new DirectoryInfo(s).Attributes & FileAttributes.ReparsePoint) != 0) continue; // não segue atalhos/junções
                Sweep(s, cutoff, st, ct);
                Directory.Delete(s, false); // só some se ficou vazia
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
    }

    private static Task<(bool Ok, string Message)> EmptyRecycleBinAsync(Action<string> log, CancellationToken ct) => Task.Run(() =>
    {
        const uint noConfirmation = 0x1, noProgressUi = 0x2, noSound = 0x4;
        int hr = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noProgressUi | noSound);
        return hr == 0 ? (true, "Lixeira esvaziada.") : (true, $"Nada para esvaziar (retorno 0x{hr:X8}).");
    }, ct);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
