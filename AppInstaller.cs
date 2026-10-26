using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32;

namespace ZyxExplode;

/// <summary>App do catálogo "Instalar apps" (pós-formatação), instalado pelo winget.</summary>
public sealed class InstallableApp : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string WingetId { get; init; } = "";
    /// <summary>Começo do nome em "Aplicativos instalados" (Painel de Controle), para saber se já está instalado.</summary>
    public string DisplayPrefix { get; init; } = "";
    public bool PreSelect { get; init; } = true;

    private bool _selected, _installed;
    private string _status = "";

    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public bool Installed
    {
        get => _installed;
        set { Set(ref _installed, value); OnChanged(nameof(CanSelect)); }
    }
    public bool CanSelect => !Installed;
    public string Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(p);
    }
}

/// <summary>
/// Catálogo de apps essenciais e instalação silenciosa pelo winget (Gerenciador de Pacotes do Windows), que baixa
/// sempre a versão oficial mais recente do site do fabricante e confere o hash do instalador.
/// </summary>
public static class AppInstaller
{
    private static InstallableApp A(string category, string name, string wingetId, string description, string prefix = "", bool preSelect = false) =>
        new() { Category = category, Name = name, WingetId = wingetId, Description = description, DisplayPrefix = prefix, PreSelect = preSelect };

    public static List<InstallableApp> Catalog()
    {
        const string nav = "Navegadores", com = "Comunicação", jog = "Jogos e lançadores", med = "Mídia e streaming",
                     esc = "Escritório e documentos", zip = "Compactadores", utl = "Utilitários do Windows", rem = "Acesso remoto",
                     hw = "Hardware e diagnóstico", seg = "Segurança e privacidade", dev = "Desenvolvimento e maker",
                     img = "Imagem e design", run = "Runtimes e bibliotecas (para jogos e programas rodarem)";
        return new List<InstallableApp>
        {
            A(nav, "Google Chrome", "Google.Chrome", "Navegador do Google.", "Google Chrome", true),
            A(nav, "Mozilla Firefox", "Mozilla.Firefox", "Navegador da Mozilla, focado em privacidade."),
            A(nav, "Brave", "Brave.Brave", "Navegador com bloqueio de anúncios embutido."),
            A(nav, "Opera", "Opera.Opera", "Navegador com VPN e barra lateral de mensageiros."),
            A(nav, "Opera GX", "Opera.OperaGX", "Opera voltado a jogadores, com limite de uso de CPU e RAM."),
            A(nav, "Vivaldi", "Vivaldi.Vivaldi", "Navegador muito personalizável."),
            A(nav, "Tor Browser", "TorProject.TorBrowser", "Navegação anônima pela rede Tor."),

            A(com, "Discord", "Discord.Discord", "Chat de voz e texto para jogar com os amigos.", "Discord", true),
            A(com, "WhatsApp", "WhatsApp.WhatsApp", "WhatsApp para Windows."),
            A(com, "Telegram", "Telegram.TelegramDesktop", "Mensageiro Telegram.", "Telegram Desktop"),
            A(com, "Zoom", "Zoom.Zoom", "Videoconferência.", "Zoom"),
            A(com, "Microsoft Teams", "Microsoft.Teams", "Reuniões e chat da Microsoft.", "Microsoft Teams"),
            A(com, "Slack", "SlackTechnologies.Slack", "Chat de equipes de trabalho.", "Slack"),
            A(com, "Signal", "OpenWhisperSystems.Signal", "Mensageiro com criptografia.", "Signal"),
            A(com, "TeamSpeak", "TeamSpeakSystems.TeamSpeakClient", "Voz para jogos, leve e sem conta obrigatória.", "TeamSpeak"),

            A(jog, "Steam", "Valve.Steam", "Loja e biblioteca de jogos da Valve.", "Steam", true),
            A(jog, "Epic Games Launcher", "EpicGames.EpicGamesLauncher", "Loja da Epic, com jogos grátis toda semana.", "Epic Games Launcher"),
            A(jog, "GOG Galaxy", "GOG.Galaxy", "Loja e biblioteca da GOG, juntando outros lançadores.", "GOG GALAXY"),
            A(jog, "Ubisoft Connect", "Ubisoft.Connect", "Lançador dos jogos da Ubisoft.", "Ubisoft Connect"),
            A(jog, "EA app", "ElectronicArts.EADesktop", "Lançador dos jogos da EA.", "EA app"),
            A(jog, "Battle.net", "Blizzard.BattleNet", "Lançador dos jogos da Blizzard.", "Battle.net"),
            A(jog, "Playnite", "Playnite.Playnite", "Biblioteca única para todos os lançadores.", "Playnite"),
            A(jog, "Heroic Games Launcher", "HeroicGamesLauncher.HeroicGamesLauncher", "Cliente livre para jogos da Epic, GOG e Amazon.", "Heroic Games Launcher"),

            A(med, "VLC", "VideoLAN.VLC", "Player de vídeo e música que abre qualquer formato.", "VLC media player", true),
            A(med, "MPC-HC", "clsid2.mpc-hc", "Player de vídeo leve.", "MPC-HC"),
            A(med, "PotPlayer", "Daum.PotPlayer", "Player de vídeo completo.", "PotPlayer"),
            A(med, "K-Lite Codec Pack", "CodecGuide.K-LiteCodecPack.Standard", "Codecs para o Windows abrir mais formatos de vídeo.", "K-Lite Codec Pack"),
            A(med, "Spotify", "Spotify.Spotify", "Música por streaming.", "Spotify"),
            A(med, "iTunes", "Apple.iTunes", "Música e backup de iPhone.", "iTunes"),
            A(med, "AIMP", "AIMP.AIMP", "Player de música leve.", "AIMP"),
            A(med, "OBS Studio", "OBSProject.OBSStudio", "Gravação de tela e transmissão ao vivo.", "OBS Studio"),
            A(med, "Streamlabs Desktop", "Streamlabs.StreamlabsDesktop", "Transmissão ao vivo com alertas e temas.", "Streamlabs"),
            A(med, "Audacity", "Audacity.Audacity", "Editor de áudio gratuito.", "Audacity"),
            A(med, "HandBrake", "HandBrake.HandBrake", "Converte e comprime vídeos.", "HandBrake"),
            A(med, "FFmpeg", "Gyan.FFmpeg", "Ferramenta de linha de comando para converter áudio e vídeo."),
            A(med, "yt-dlp", "yt-dlp.yt-dlp", "Baixa vídeos de sites (linha de comando)."),
            A(med, "ShareX", "ShareX.ShareX", "Captura de tela e gravação com muitos recursos.", "ShareX"),
            A(med, "Lightshot", "Skillbrains.Lightshot", "Captura de tela rápida com a tecla Print Screen.", "Lightshot", true),
            A(med, "Greenshot", "Greenshot.Greenshot", "Captura de tela com anotações.", "Greenshot"),

            A(esc, "LibreOffice", "TheDocumentFoundation.LibreOffice", "Pacote de escritório gratuito (Writer, Calc, Impress).", "LibreOffice"),
            A(esc, "WPS Office", "Kingsoft.WPSOffice", "Pacote de escritório parecido com o Office.", "WPS Office"),
            A(esc, "ONLYOFFICE", "ONLYOFFICE.DesktopEditors", "Editores de documentos compatíveis com o Office.", "ONLYOFFICE"),
            A(esc, "Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit", "Leitor de PDF da Adobe.", "Adobe Acrobat"),
            A(esc, "Foxit PDF Reader", "Foxit.FoxitReader", "Leitor de PDF leve.", "Foxit"),
            A(esc, "SumatraPDF", "SumatraPDF.SumatraPDF", "Leitor de PDF e e-books muito leve.", "SumatraPDF"),
            A(esc, "Notepad++", "Notepad++.Notepad++", "Editor de texto e código.", "Notepad++"),
            A(esc, "Obsidian", "Obsidian.Obsidian", "Notas em arquivos Markdown.", "Obsidian"),
            A(esc, "Notion", "Notion.Notion", "Notas, documentos e bancos de dados.", "Notion"),
            A(esc, "Google Drive", "Google.GoogleDrive", "Sincroniza arquivos com o Google Drive.", "Google Drive"),
            A(esc, "Dropbox", "Dropbox.Dropbox", "Sincroniza arquivos com o Dropbox.", "Dropbox"),

            A(zip, "7-Zip", "7zip.7zip", "Compactador gratuito (zip, rar, 7z). Abre e cria arquivos compactados.", "7-Zip", true),
            A(zip, "WinRAR", "RARLab.WinRAR", "Compactador clássico (pago após o teste). Escolha ele ou o 7-Zip.", "WinRAR"),
            A(zip, "NanaZip", "M2Team.NanaZip", "Compactador moderno, baseado no 7-Zip.", "NanaZip"),
            A(zip, "PeaZip", "Giorgiotani.Peazip", "Compactador gratuito com muitos formatos.", "PeaZip"),

            A(utl, "PowerToys", "Microsoft.PowerToys", "Ferramentas extras da Microsoft (réguas de janelas, renomear em massa, seletor de cor).", "PowerToys"),
            A(utl, "Everything", "voidtools.Everything", "Busca instantânea de arquivos no PC inteiro.", "Everything"),
            A(utl, "WizTree", "AntibodySoftware.WizTree", "Mostra o que está ocupando o disco, bem rápido.", "WizTree"),
            A(utl, "WinDirStat", "WinDirStat.WinDirStat", "Mapa visual do uso do disco.", "WinDirStat"),
            A(utl, "TreeSize Free", "JAMSoftware.TreeSize.Free", "Tamanho de cada pasta do disco.", "TreeSize"),
            A(utl, "Revo Uninstaller", "RevoUninstaller.RevoUninstaller", "Desinstala programas e limpa os restos.", "Revo Uninstaller"),
            A(utl, "Bulk Crap Uninstaller", "Klocman.BulkCrapUninstaller", "Desinstala vários programas de uma vez.", "BCUninstaller"),
            A(utl, "BleachBit", "BleachBit.BleachBit", "Limpeza de arquivos temporários e rastros.", "BleachBit"),
            A(utl, "Rufus", "Rufus.Rufus", "Cria pendrive de instalação do Windows.", "Rufus"),
            A(utl, "Ventoy", "Ventoy.Ventoy", "Pendrive com vários instaladores (basta copiar os ISOs).", "Ventoy"),
            A(utl, "balenaEtcher", "Balena.Etcher", "Grava imagens ISO em pendrive e cartão SD.", "balenaEtcher"),
            A(utl, "qBittorrent", "qBittorrent.qBittorrent", "Cliente de torrent sem anúncios.", "qBittorrent"),
            A(utl, "f.lux", "flux.flux", "Ajusta a cor da tela à noite.", "f.lux"),
            A(utl, "Windows Terminal", "Microsoft.WindowsTerminal", "Terminal moderno do Windows."),
            A(utl, "PowerShell 7", "Microsoft.PowerShell", "Versão atual do PowerShell.", "PowerShell 7"),
            A(utl, "Process Explorer", "Microsoft.Sysinternals.ProcessExplorer", "Gerenciador de tarefas avançado da Sysinternals."),
            A(utl, "Autoruns", "Microsoft.Sysinternals.Autoruns", "Tudo que abre junto com o Windows, em detalhe (Sysinternals)."),

            A(rem, "AnyDesk", "AnyDeskSoftwareGmbH.AnyDesk", "Acesso remoto para dar suporte ao PC do cliente.", "AnyDesk"),
            A(rem, "TeamViewer", "TeamViewer.TeamViewer", "Acesso remoto e reuniões.", "TeamViewer"),
            A(rem, "RustDesk", "RustDesk.RustDesk", "Acesso remoto de código aberto.", "RustDesk"),
            A(rem, "PuTTY", "PuTTY.PuTTY", "Cliente SSH e serial.", "PuTTY"),
            A(rem, "WinSCP", "WinSCP.WinSCP", "Transferência de arquivos por SFTP/SCP.", "WinSCP"),
            A(rem, "FileZilla", "TimKosse.FileZilla.Client", "Cliente de FTP.", "FileZilla"),

            A(hw, "CPU-Z", "CPUID.CPU-Z", "Detalhes de processador, placa-mãe e memória.", "CPUID CPU-Z"),
            A(hw, "HWMonitor", "CPUID.HWMonitor", "Temperaturas, tensões e ventoinhas.", "CPUID HWMonitor"),
            A(hw, "GPU-Z", "TechPowerUp.GPU-Z", "Detalhes e sensores da placa de vídeo.", "TechPowerUp GPU-Z"),
            A(hw, "HWiNFO", "REALiX.HWiNFO", "Informações e sensores de todo o hardware.", "HWiNFO"),
            A(hw, "CrystalDiskInfo", "CrystalDewWorld.CrystalDiskInfo", "Saúde de HD e SSD (S.M.A.R.T.).", "CrystalDiskInfo"),
            A(hw, "CrystalDiskMark", "CrystalDewWorld.CrystalDiskMark", "Teste de velocidade de HD e SSD.", "CrystalDiskMark"),
            A(hw, "MSI Afterburner", "Guru3D.Afterburner", "Overclock e monitoramento da placa de vídeo.", "MSI Afterburner"),
            A(hw, "OCCT", "OCBase.OCCT", "Teste de estabilidade de CPU, GPU e fonte.", "OCCT"),
            A(hw, "Prime95", "mersenne.prime95", "Teste de estresse de CPU."),
            A(hw, "Process Lasso", "BitSum.ProcessLasso", "Prioridade e afinidade de CPU por programa.", "Process Lasso"),
            A(hw, "Display Driver Uninstaller", "Wagnardsoft.DisplayDriverUninstaller", "Remove por completo o driver da placa de vídeo.", "Display Driver Uninstaller"),
            A(hw, "NVCleanstall", "TechPowerUp.NVCleanstall", "Instala o driver NVIDIA sem os extras.", "NVCleanstall"),
            A(hw, "Intel Driver & Support Assistant", "Intel.IntelDriverAndSupportAssistant", "Atualiza drivers de componentes Intel.", "Intel"),

            A(seg, "Malwarebytes", "Malwarebytes.Malwarebytes", "Verificação e remoção de malware.", "Malwarebytes"),
            A(seg, "Bitwarden", "Bitwarden.Bitwarden", "Gerenciador de senhas.", "Bitwarden"),
            A(seg, "KeePassXC", "KeePassXCTeam.KeePassXC", "Gerenciador de senhas offline.", "KeePassXC"),
            A(seg, "Proton VPN", "Proton.ProtonVPN", "VPN com plano gratuito.", "Proton VPN"),
            A(seg, "Cloudflare WARP", "Cloudflare.Warp", "Conexão mais segura e DNS rápido.", "Cloudflare WARP"),

            A(dev, "Visual Studio Code", "Microsoft.VisualStudioCode", "Editor de código.", "Microsoft Visual Studio Code"),
            A(dev, "Git", "Git.Git", "Controle de versão.", "Git"),
            A(dev, "Python 3.12", "Python.Python.3.12", "Linguagem Python.", "Python 3.12"),
            A(dev, "Node.js (LTS)", "OpenJS.NodeJS.LTS", "JavaScript fora do navegador.", "Node.js"),
            A(dev, "Docker Desktop", "Docker.DockerDesktop", "Contêineres.", "Docker Desktop"),
            A(dev, "VirtualBox", "Oracle.VirtualBox", "Máquinas virtuais.", "Oracle VM VirtualBox"),
            A(dev, "Wireshark", "WiresharkFoundation.Wireshark", "Análise de pacotes de rede.", "Wireshark"),
            A(dev, "Postman", "Postman.Postman", "Teste de APIs.", "Postman"),
            A(dev, "Arduino IDE", "ArduinoSA.IDE.stable", "Programa placas Arduino (traz os drivers das oficiais).", "Arduino IDE"),
            A(dev, "Thonny", "Thonny.Thonny", "Editor Python simples, bom para MicroPython.", "Thonny"),
            A(dev, "KiCad", "KiCad.KiCad", "Projeto de placas de circuito.", "KiCad"),
            A(dev, "Ultimaker Cura", "Ultimaker.Cura", "Fatiador para impressora 3D.", "UltiMaker Cura"),
            A(dev, "PrusaSlicer", "Prusa3D.PrusaSlicer", "Fatiador para impressora 3D.", "PrusaSlicer"),

            A(img, "GIMP", "GIMP.GIMP", "Editor de imagens gratuito.", "GIMP"),
            A(img, "Paint.NET", "dotPDN.PaintDotNet", "Editor de imagens simples e rápido.", "paint.net"),
            A(img, "Krita", "KDE.Krita", "Pintura digital e ilustração.", "Krita"),
            A(img, "Inkscape", "Inkscape.Inkscape", "Desenho vetorial.", "Inkscape"),
            A(img, "Blender", "BlenderFoundation.Blender", "Modelagem 3D e animação.", "Blender"),
            A(img, "IrfanView", "IrfanSkiljan.IrfanView", "Visualizador de imagens leve.", "IrfanView"),
            A(img, "XnView MP", "XnSoft.XnViewMP", "Visualizador e organizador de fotos.", "XnView"),
            A(img, "ImageGlass", "DuongDieuPhap.ImageGlass", "Visualizador de imagens moderno.", "ImageGlass"),
            A(img, "Figma", "Figma.Figma", "Design de interfaces.", "Figma"),

            A(run, "Visual C++ Redistributable 2015-2022 (x64)", "Microsoft.VCRedist.2015+.x64", "Biblioteca que muitos jogos e programas exigem.", "Microsoft Visual C++ 2015-2022 Redistributable (x64)"),
            A(run, "Visual C++ Redistributable 2015-2022 (x86)", "Microsoft.VCRedist.2015+.x86", "A versão de 32 bits, usada por programas antigos.", "Microsoft Visual C++ 2015-2022 Redistributable (x86)"),
            A(run, ".NET Desktop Runtime 8", "Microsoft.DotNet.DesktopRuntime.8", "Necessário para programas feitos em .NET 8.", "Microsoft Windows Desktop Runtime - 8"),
            A(run, "DirectX (runtime)", "Microsoft.DirectX", "Bibliotecas DirectX antigas que alguns jogos pedem.", "Microsoft DirectX"),
            A(run, "Java (Temurin 21 JRE)", "EclipseAdoptium.Temurin.21.JRE", "Java para programas e para o Minecraft.", "Eclipse Temurin JRE"),
        };
    }

    /// <summary>Seleções prontas (por ID do winget) para o técnico marcar de uma vez.</summary>
    public static readonly (string Label, string[] Ids)[] Profiles =
    {
        ("CLIENTE BÁSICO", new[] { "Google.Chrome", "7zip.7zip", "VideoLAN.VLC", "Adobe.Acrobat.Reader.64-bit", "WhatsApp.WhatsApp",
                                   "AnyDeskSoftwareGmbH.AnyDesk", "Skillbrains.Lightshot", "Microsoft.VCRedist.2015+.x64" }),
        ("GAMER", new[] { "Valve.Steam", "Discord.Discord", "EpicGames.EpicGamesLauncher", "Microsoft.VCRedist.2015+.x64",
                          "Microsoft.VCRedist.2015+.x86", "Microsoft.DirectX", "Microsoft.DotNet.DesktopRuntime.8", "7zip.7zip",
                          "Guru3D.Afterburner", "OBSProject.OBSStudio" }),
        ("TÉCNICO", new[] { "Google.Chrome", "7zip.7zip", "AnyDeskSoftwareGmbH.AnyDesk", "TeamViewer.TeamViewer", "CPUID.CPU-Z",
                            "TechPowerUp.GPU-Z", "CPUID.HWMonitor", "CrystalDewWorld.CrystalDiskInfo", "CrystalDewWorld.CrystalDiskMark",
                            "Rufus.Rufus", "Ventoy.Ventoy", "Notepad++.Notepad++", "voidtools.Everything", "WinDirStat.WinDirStat",
                            "Microsoft.Sysinternals.ProcessExplorer", "Microsoft.Sysinternals.Autoruns",
                            "Wagnardsoft.DisplayDriverUninstaller", "PuTTY.PuTTY" }),
    };

    public static string WingetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");

    public static bool WingetAvailable => File.Exists(WingetPath);

    /// <summary>Nomes em "Aplicativos instalados" (máquina 64/32 bits e usuário atual).</summary>
    public static HashSet<string> InstalledNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
                                             (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n && !string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
                }
            }
            catch { }
        }
        return names;
    }

    public static void RefreshInstalled(IEnumerable<InstallableApp> apps)
    {
        var names = InstalledNames();
        foreach (var a in apps)
        {
            string prefix = string.IsNullOrEmpty(a.DisplayPrefix) ? a.Name : a.DisplayPrefix;
            a.Installed = names.Any(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (a.Installed) a.Selected = false;
        }
    }

    /// <summary>Instala um app em silêncio. Retorna (ok, mensagem curta).</summary>
    public static async Task<(bool Ok, string Message)> InstallAsync(InstallableApp app, CancellationToken ct, bool upgrade = false)
    {
        var psi = new ProcessStartInfo(WingetPath,
            $"{(upgrade ? "upgrade" : "install")} --id {app.WingetId} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o winget.");
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }
        string output = await outTask + await errTask;

        return p.ExitCode switch
        {
            0 => (true, "Instalado."),
            unchecked((int)0x8A15002B) or unchecked((int)0x8A150061) => (true, "Já estava instalado."),
            unchecked((int)0x8A150014) => (false, "Pacote não encontrado no winget."),
            unchecked((int)0x8A150008) => (false, "Falha no download: confira a internet e tente de novo."),
            _ => (false, $"Falhou (código 0x{p.ExitCode:X8}). {LastLine(output)}".Trim()),
        };
    }

    /// <summary>Última linha útil da saída do winget (sem as barras de progresso).</summary>
    private static string LastLine(string output) =>
        output.Split('\n', '\r').Select(l => l.Trim())
              .LastOrDefault(l => l.Length > 3 && !l.All(c => "-\\|/█▒ ".Contains(c)) && !l.Contains("KB /") && !l.Contains("MB /")) ?? "";
}
