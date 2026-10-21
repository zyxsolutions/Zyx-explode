# Avisos de terceiros

O ZYX EXPLODE usa e distribui os componentes abaixo. Cada um continua sob a licença do seu autor.

## Projeto de origem
- **RESET EXPLODE v0.16** — Copyright (c) 2026 Gustavo (GustavoReset), licença MIT.
  <https://github.com/GustavoReset/ResetExplode>. O texto da licença e o aviso de copyright estão em [LICENSE](LICENSE).

## Bibliotecas (pacotes NuGet)
| Componente | Licença | Uso |
|---|---|---|
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.6 | MPL 2.0 | Sensores de hardware |
| [Microsoft.Diagnostics.Tracing.TraceEvent](https://github.com/microsoft/perfview) | MIT | Medição de FPS por eventos ETW |
| [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) (Direct3D 11/12, DXC, D3DCompiler) | MIT | Stress de GPU e benchmarks |
| System.Management, System.Security.Cryptography.ProtectedData | MIT (Microsoft) | WMI e cifra da chave de API |

A MPL 2.0 exige que o código-fonte da LibreHardwareMonitorLib (sem alterações, aqui) continue disponível sob essa licença:
<https://github.com/LibreHardwareMonitor/LibreHardwareMonitor>.

## Drivers incluídos (pasta `Drivers/`)
Pacotes oficiais, sem modificação, de WCH (CH340), Silicon Labs (CP210x), FTDI e Arduino. **Não fazem parte da licença MIT**:
pertencem aos fabricantes e seguem os termos de cada um. Origens, versões e SHA-256 em [Drivers/LEIA-ME.md](Drivers/LEIA-ME.md).

## Serviços externos usados em tempo de execução
- HowManyFPS API (opcional, com chave do usuário) — estimativas de FPS.
- speed.cloudflare.com — teste de velocidade da aba Rede.
- iCUE SDK da Corsair (baixado só com confirmação do usuário), OpenRGB e Razer Chroma — pontes de iluminação.
