using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;

namespace ZyxExplode;

/// <summary>Resultado da checagem: há versão nova? Lê o version.txt publicado junto da release "latest" no GitHub.</summary>
public sealed record UpdateInfo(Version Latest, Version Current, string? InstallerSha, string? ExeSha)
{
    public bool IsNewer => Latest > Current;
}

/// <summary>
/// Atualização em um clique. Instalado (Arquivos de Programas): baixa o ZyxExplode-Setup.exe e roda em modo silencioso, que fecha o
/// programa, atualiza e abre de novo. Portátil (.exe solto): baixa o ZyxExplode.exe novo e troca o arquivo depois que o programa fecha.
/// Os downloads são conferidos pelo SHA-256 publicado no version.txt.
/// </summary>
public static class AppUpdater
{
    private const string BaseUrl = "https://github.com/zyxsolutions/zyx-explode/releases/download/latest/";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    static AppUpdater() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("ZyxExplode-Updater");

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
        }
    }

    public static string CurrentText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// <summary>True quando o programa foi instalado pelo instalador (roda de Arquivos de Programas).</summary>
    public static bool IsInstalled
    {
        get
        {
            string dir = AppContext.BaseDirectory;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return dir.StartsWith(pf, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(pf86, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Consulta a versão publicada. Lança exceção se estiver sem internet ou se o arquivo não existir.</summary>
    public static async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        string text = await Http.GetStringAsync(BaseUrl + "version.txt?t=" + DateTime.UtcNow.Ticks, ct);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !Version.TryParse(lines[0], out var latest))
            throw new InvalidDataException("O arquivo de versão publicado está em um formato inesperado.");
        return new UpdateInfo(latest, CurrentVersion,
            lines.Length > 1 ? lines[1].ToLowerInvariant() : null,
            lines.Length > 2 ? lines[2].ToLowerInvariant() : null);
    }

    /// <summary>Baixa e inicia a atualização. Em caso de sucesso, o programa vai fechar logo em seguida.</summary>
    public static async Task<string?> InstallAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct = default)
    {
        try
        {
            bool installed = IsInstalled;
            string name = installed ? "ZyxExplode-Setup.exe" : "ZyxExplode.exe";
            string? expected = installed ? info.InstallerSha : info.ExeSha;

            string dir = Path.Combine(Path.GetTempPath(), "ZyxExplode-update");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, name);
            if (File.Exists(file)) File.Delete(file);

            using (var resp = await Http.GetAsync(BaseUrl + name, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(file);
                var buf = new byte[81920];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    if (total > 0) progress.Report(done * 100.0 / total);
                }
            }

            if (expected != null)
            {
                string actual;
                await using (var fs = File.OpenRead(file))
                    actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
                if (actual != expected)
                {
                    File.Delete(file);
                    return "O arquivo baixado não confere com o publicado (SHA-256 diferente). Nada foi instalado.";
                }
            }

            if (installed) StartInstaller(file);
            else StartSelfReplace(file);
            return null;
        }
        catch (OperationCanceledException) { return "Atualização cancelada."; }
        catch (Exception ex) { return "Não foi possível atualizar: " + ex.Message; }
    }

    private static void StartInstaller(string setup)
    {
        // O instalador fecha este programa, atualiza e abre a versão nova (veja [Run] em ZyxExplode.iss).
        Process.Start(new ProcessStartInfo
        {
            FileName = setup,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true,
            Verb = "runas",
        });
    }

    private static void StartSelfReplace(string newExe)
    {
        string current = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do programa desconhecido.");
        string script = Path.Combine(Path.GetDirectoryName(newExe)!, "aplicar.cmd");
        int pid = Environment.ProcessId;
        // Espera este processo fechar, troca o arquivo (com algumas tentativas) e abre o novo.
        File.WriteAllText(script,
            "@echo off\r\nchcp 65001 >nul\r\n" +
            $"set /a n=0\r\n" +
            $":wait\r\n" +
            $"tasklist /fi \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
            "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
            ":copy\r\n" +
            $"copy /y \"{newExe}\" \"{current}\" >nul 2>&1\r\n" +
            "if errorlevel 1 (set /a n+=1 & if %n% lss 15 (timeout /t 1 /nobreak >nul & goto copy))\r\n" +
            $"start \"\" \"{current}\"\r\n" +
            $"del \"{newExe}\" >nul 2>&1\r\n" +
            "(goto) 2>nul & del \"%~f0\"\r\n",
            new System.Text.UTF8Encoding(false));
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}
