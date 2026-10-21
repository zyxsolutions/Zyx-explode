# Assina arquivos com o signtool usando um certificado .pfx (guardado em segredos do GitHub, nunca no repositório).
# Uso (no workflow):  pwsh tools/sign.ps1 dist\ZyxExplode.exe installer\Output\ZyxExplode-Setup.exe
# Variáveis de ambiente:
#   CODESIGN_PFX_BASE64    certificado .pfx em Base64
#   CODESIGN_PFX_PASSWORD  senha do .pfx
#   CODESIGN_TIMESTAMP_URL (opcional) servidor de carimbo de data/hora; padrão: DigiCert
# Sem CODESIGN_PFX_BASE64 o script não faz nada (os arquivos saem sem assinatura) e termina com sucesso.
param([Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)][string[]]$Files)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:CODESIGN_PFX_BASE64)) {
    Write-Host "Sem certificado configurado: arquivos mantidos SEM assinatura digital."
    exit 0
}

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName | Select-Object -Last 1
if (-not $signtool) { throw "signtool.exe não encontrado (Windows SDK)." }

$pfx = Join-Path $env:RUNNER_TEMP 'codesign.pfx'
[IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:CODESIGN_PFX_BASE64))
$ts = if ($env:CODESIGN_TIMESTAMP_URL) { $env:CODESIGN_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
try {
    foreach ($f in $Files) {
        & $signtool.FullName sign /f $pfx /p $env:CODESIGN_PFX_PASSWORD /fd SHA256 /tr $ts /td SHA256 /d "Zyx Explode" $f
        if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $f" }
        & $signtool.FullName verify /pa $f
        if ($LASTEXITCODE -ne 0) { throw "Assinatura inválida em $f" }
        Write-Host "Assinado: $f"
    }
}
finally {
    Remove-Item $pfx -Force -ErrorAction SilentlyContinue   # nunca deixa o certificado no disco do runner
}
