@echo off
rem Gera o ZYX EXPLODE pronto para levar a outro PC (nao precisa instalar o .NET la).
rem Saida: ..\ZyxExplode-dist\ZyxExplode.exe + LEIA-ME.txt
rem Sempre recompila do zero a partir do codigo atual desta pasta.
cd /d "%~dp0"
echo Fechando o ZyxExplode se estiver aberto ...
taskkill /f /im ZyxExplode.exe >nul 2>&1
echo Gerando ZyxExplode.exe a partir do codigo atual ...
rmdir /s /q obj\Release >nul 2>&1
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "..\ZyxExplode-dist" -nologo -v q
if errorlevel 1 (
    echo.
    echo FALHOU. Veja os erros acima. O .exe antigo NAO foi atualizado.
    pause
    exit /b 1
)
copy /y "LEIA-ME.txt" "..\ZyxExplode-dist\LEIA-ME.txt" >nul
echo.
echo Pronto: %~dp0..\ZyxExplode-dist
for %%F in ("..\ZyxExplode-dist\ZyxExplode.exe") do echo Arquivo gerado em %%~tF (confira se e de agora^)
pause
