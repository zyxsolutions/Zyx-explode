## Novidades da v0.18.1

**O programa ficou bem mais leve com a janela aberta**
- Os sensores não são mais lidos na thread da janela: a leitura pesada roda em segundo plano e a tela não engasga.
- Placa-mãe, discos (SMART) e rede são lidos a cada 15 s (ou a cada segundo só quando as páginas Sensores ou Discos estão abertas). CPU, GPU e memória
  continuam a cada segundo.
- Com a janela minimizada (e sem teste, overlay ou análise de gargalo rodando), as leituras caem para uma a cada 10 s.
- O rastreio de FPS (ETW), que escuta os eventos gráficos de todos os programas, agora só liga quando você abre Gargalo ou Overlay, e desliga depois.
- A consulta de FPS à API HowManyFPS só acontece ao abrir a página Jogos (antes disparava ao abrir o programa, para todos os jogos).

## Novidades da v0.18

**Atualização em um clique, dentro do programa**
- Botão no canto superior direito: quando sai versão nova, ele mostra **ATUALIZAR PARA x.y.z**. Um clique baixa, fecha o programa, atualiza e abre de novo,
  mantendo configurações e históricos. Sem desinstalar nem baixar do GitHub na mão.
- Confere o SHA-256 do arquivo baixado antes de instalar. Funciona instalado (usa o instalador em modo silencioso) e com o `.exe` solto (troca o arquivo).
- Verifica sozinho alguns segundos depois de abrir. Clique no botão a qualquer momento para verificar de novo.

**Nova página · ZYX DESEMPENHO › Processos (estilo Process Lasso)**
- Lista os programas com **CPU, RAM, prioridade e núcleos**, com busca. Atualiza sozinha.
- Muda a **prioridade de CPU**, os **núcleos permitidos (afinidade)** e a **prioridade de disco** de qualquer programa, e encerra programas travados.
- **Regras salvas**: marque "Lembrar" e o ZYX EXPLODE reaplica os ajustes sempre que o programa abrir de novo. As regras valem enquanto o
  ZYX EXPLODE estiver aberto (não é um serviço do Windows).

## Novidades da v0.17

**Nova página · ZYX FERRAMENTAS › Memória (estilo RAMMap)**
- Mostra como o Windows está usando a RAM: **em uso, modificada, standby (cache) e livre**, em barra e em números, com o standby
  separado por prioridade (0 a 7), cache do sistema, commit e pools do kernel. Atualiza sozinha a cada 2 s.
- **Leitura do estado** em português (RAM quase cheia, standby grande, commit alto, leitura de disco por segundo, pool não paginado alto).
- Botões para **esvaziar**: working sets, working set do sistema, lista modificada, standby e standby de prioridade 0, mais ESVAZIAR TUDO,
  com o quanto de RAM voltou a ficar disponível em cada um.
- Tabela de **processos por memória** (na RAM agora, privada e pico), com busca e opção de esvaziar o working set do processo selecionado.
- Precisa rodar como administrador (como o resto do programa). Não inclui o detalhamento por tipo de página (mapped file, page table...)
  do RAMMap, que depende de chamadas internas não documentadas do Windows.

**Nova página · ZYX FERRAMENTAS › Manutenção**
- SFC, DISM (checar, reparar, limpar componentes), chkdsk sem reiniciar, limpar DNS, renovar IP, Winsock e TCP/IP, plano de energia,
  relatório de bateria, temporários antigos, lixeira e Limpeza de Disco do Windows.
- **O comando exato aparece antes de rodar**; as tarefas rodam uma de cada vez, com saída ao vivo e botão CANCELAR.
- Avisos antes de rodar o que demora, derruba a rede, pede reinício ou apaga de vez.

**Instalar apps · catálogo grande**
- Mais de 120 apps em categorias (navegadores, comunicação, jogos, mídia, escritório, compactadores, utilitários, acesso remoto,
  hardware e diagnóstico, segurança, desenvolvimento e maker, imagem, runtimes), com **busca**.
- **Seleções prontas**: CLIENTE BÁSICO, GAMER e TÉCNICO.

**Nova página · ZYX DESEMPENHO › Teste de monitor**
- Seis testes em tela cheia: cores sólidas (pixels mortos, vazamento de luz), gradientes (bandas), movimento (rastro e fantasma),
  padrões de pixel, grade e geometria, e taxa de atualização com contagem de quadros pulados.

**Sistema › Processador (estilo CPU-Z)**
- Família, modelo e passo, clock atual, barramento e multiplicador estimado, blocos de cache L1/L2/L3 e conjuntos de instruções
  (SSE, AVX, AVX2, AVX-512, FMA, AES...).

**Correção**
- Sistema › Placa de vídeo: a memória passa a mostrar o tamanho real (antes o WMI limitava em 4 GB).

## Novidades da v0.16

**Nova página · ZYX FERRAMENTAS › Inicialização**
- Lista os programas que abrem junto com o Windows: chaves Run do registro (usuário, todos os usuários, 32 e 64 bits)
  e pastas Inicializar, com descrição, fabricante e aviso quando o arquivo do programa não existe mais.
- Interruptor para ativar/desativar, igual ao Gerenciador de Tarefas: a entrada continua lá e pode ser religada.
- Botão **REMOVER** com confirmação; o programa continua instalado e tudo pode ser desfeito em **Restaurar removidos**.

**Nova página · ZYX FERRAMENTAS › Instalar apps**
- Os essenciais depois de formatar o PC: Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC.
- Instala pelo winget (instalador de pacotes da Microsoft que já vem no Windows): versão oficial mais recente,
  sem janelas, um app de cada vez, com o resultado de cada um na lista.
- Mostra o que já está instalado; se o winget ainda não existir (PC recém-formatado), um botão abre a Microsoft Store
  para atualizar o "Instalador de Aplicativo".

**Nova categoria · ZYX MAKER** (Arduino, ESP32 e conversores USB-serial)
- **Portas COM**: a limpeza de dispositivos fantasmas e de números COM presos saiu de Otimização e ganhou página própria.
- **Drivers seriais**: diagnóstico das placas com CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino
  (sem driver, código 10, código 43, FTDI falsificado travado em PID 0000), com a orientação de cada caso.
- **REINSTALAR** remove o dispositivo e faz o Windows detectar de novo com o melhor driver.
- Lista as versões de driver guardadas no Windows para cada chip, com **REMOVER** (backup antes): volta para uma versão
  anterior quando a nova não funciona com clones, ou desinstala de vez. **Restaurar removidos** desfaz.
- **Drivers incluídos no app**: CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, oficiais e assinados, instalam sem internet
  (ideal logo depois de formatar), com a placa conectada ou não. Também abre a página oficial de cada fabricante.
- **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que prendem a porta COM e limpar o cache de índices
  quando a IDE trava em "Downloading index".

**Novo benchmark · Ray tracing (GPU)**
- Teste em Direct3D 12 com DXR 1.1 que usa os núcleos de ray tracing (RTX 20+, Radeon RX 6000+, Intel Arc): 4 mil objetos,
  reflexos de reflexos, sombras suaves, oclusão de ambiente e iluminação indireta, com a cena refeita a cada frame.
- Feito para comparar com confiança: aquecimento sem medir, 3 passadas iguais, nota pela mediana e variação entre passadas
  (selo de "resultado confiável" abaixo de 3%); tempo de frame medido pelo relógio da própria GPU.
- Presets Normal, Alta e Extrema em 1080p, 1440p e 4K; histórico comparando só resultados do mesmo preset e resolução.
- Referência: RX 9070, Alta 1440p = 878 pts (87,8 FPS), variação 0,5%.

**Overlay personalizável**
- Na aba Overlay, escolha o que aparece: FPS, frametime, 1% low, gráfico; GPU (uso, temperatura, hotspot, clock do núcleo,
  clock da memória, consumo, tensão, ventoinha, limitador NVIDIA); VRAM (usada, %, temperatura); CPU (uso, núcleo mais
  ocupado, temperatura, clock médio, clock máximo, consumo); RAM (GB, %); nome do jogo e hora. Atalhos SÓ FPS, PADRÃO e TUDO.
- Cada componente vira uma linha só com os itens escolhidos; a escolha fica salva e vale na hora.

**ZYX JOGOS**
- Novo atalho em Extras: **-allow_third_party_software** (CS2), que libera captura de jogo do OBS, ReShade e overlays
  bloqueados pelo modo confiável; o aviso sobre o Fator de Confiança aparece ao passar o mouse.

**Abertura mais rápida**
- A janela aparece em ~0,5 s (antes ~4,6 s). Informações do sistema, sensores e placa de vídeo carregam em paralelo,
  em segundo plano, e cada página é preenchida quando a sua parte fica pronta.
- O aviso "sem sensor da CPU" não aparece mais por engano enquanto o hardware ainda está sendo detectado.

**Correção · Overlay de jogos**
- O overlay agora fica por cima de jogos em janela sem borda que também se marcam como "sempre no topo"
  (ex.: GTA V e Cyberpunk 2077), inclusive depois de Alt+Tab.
- Aparece no monitor onde o jogo está, não só no monitor principal.
