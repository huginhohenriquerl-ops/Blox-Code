# Log de atualizações do Blox Code

Todas as versões do Blox Code, da mais nova para a mais antiga. Quem tem o Blox Code instalado
recebe cada versão automaticamente na próxima vez que abrir.

## 1.0.2 — Robustez antes do port para Linux

Última varredura no código antes de começar a versão para Linux. Nada muda na aparência; o foco é
o Blox Code não travar nem fechar sozinho em situações comuns.

### Erros que podiam fechar o app

- **Área de transferência ocupada.** Se outro programa estivesse usando a área de transferência
  (gerenciadores de clipboard, o próprio navegador), o **Export JSON** do Fast Flag Editor dava erro.
  Agora ele tenta de novo por um instante e, se não der, avisa "The clipboard is busy — try again".
- **Abrir pastas e links.** "Mods folder", "Open logs", "Open install folder" e os links do app
  davam erro se a pasta não existisse ou não houvesse navegador padrão. Agora a pasta é criada se
  faltar, e qualquer outro problema vira uma mensagem.
- **Erro logo na abertura.** Se a pasta de dados (`%LocalAppData%\BloxCode`) não pudesse ser criada,
  o app fechava antes de conseguir mostrar ou registrar o erro. Agora o tratamento de erros entra
  primeiro, e você vê a mensagem.
- **Erros fora da janela principal.** Falhas em tarefas de fundo não eram registradas em lugar
  nenhum. Agora vão para o log, o que facilita achar a causa de qualquer problema.
- **Mensagem de erro que falha.** Se a própria janela de erro falhasse ao abrir, o app podia entrar
  num ciclo de erros. Agora isso é registrado e o ciclo é interrompido.

### Abrir o Roblox

- **Links com espaços ou aspas.** O link do jogo agora é repassado ao Roblox como um único argumento
  entre aspas quando precisa, então não pode ser quebrado em pedaços.
- **Canal inválido.** Um canal digitado com espaço, barra ou outro caractere estranho em
  Behaviour virava um endereço errado. Agora é ignorado (fica registrado no log) e o Blox Code usa
  o `production`.
- **Instalador do WebView2 travado.** Se o instalador do WebView2 (que o Roblox usa) travasse, o
  launch ficava esperando para sempre. Agora ele espera no máximo 3 minutos e segue; o Roblox abre
  mesmo assim.
- **Conexão que não responde.** Um servidor que não aceita a conexão agora falha em 10 segundos, em
  vez dos ~21 segundos do Windows, e o Blox Code passa para o próximo.

### Instalação e Fast Flags

- **Atalhos.** Se um atalho do menu Iniciar ou da área de trabalho não pudesse ser criado, a
  instalação parava no meio. Agora o atalho com problema é pulado (fica no log) e a instalação termina.
- **Importar arquivo JSON.** Abrir um arquivo que não pode ser lido, ou um arquivo enorme por engano,
  dava erro ou travava o editor. Agora aparece uma mensagem; arquivos acima de 2 MB são recusados.

### Novidade pequena

- O aviso **"Updated to vX"** na tela inicial agora tem o botão **What's new**, que abre as notas
  daquela versão no GitHub.

## 1.0.1 — Estabilidade

### Rede e downloads

- **Travamentos de até 10 minutos.** A escolha do servidor de download, a consulta da versão do
  Roblox e o manifesto de pacotes agora têm tempo-limite curto (8, 15 e 30 segundos).
- **Download parado.** Um download que parasse de receber dados ficava travado para sempre. Agora,
  sem dados por 30 segundos, ele é tentado de novo, até 3 vezes.
- **Sem internet.** Aparecia como "Channel 'production' is not available". Agora aparece como erro
  de rede.

### Abertura e atualização

- **Processo invisível.** Um erro antes da primeira janela deixava o Blox Code rodando sem janela.
  Agora ele mostra o erro e fecha.
- **Atualização que falha.** Se o Blox Code não conseguisse se substituir, o que você pediu (entrar
  no jogo) se perdia. Agora acontece mesmo assim, na versão atual, e a atualização tenta de novo depois.
- **Uma janela de menu por vez.** Abrir o Blox Code de novo traz o menu já aberto para a frente,
  como no Bloxstrap, em vez de abrir um segundo que sobrescreveria as configurações do primeiro.
- **Launch na fila.** Um segundo launch ao mesmo tempo mostra "Waiting for another Blox Code launch
  to finish".
- **Tema do sistema.** No tema "System", o Blox Code troca de claro para escuro junto com o Windows.

### Arquivos e configurações

- **Configurações sem corromper.** Settings e estado são gravados num arquivo temporário e trocados
  de uma vez, então desligar o PC no meio não estraga nada. Um arquivo já estragado é guardado como
  `.corrupt` em vez de ser substituído pelas configurações padrão.
- **Desinstalação.** O comando de desinstalação não apagava a pasta de instalação (o `timeout` do
  Windows falha sem console). Agora apaga.
- **Roblox aberto.** Reinstalar com o Roblox aberto quebrava a instalação pela metade. Agora o Blox
  Code avisa "Close Roblox and try again" antes de apagar qualquer coisa.
- **Mods em uso.** Um mod que o Roblox estivesse usando impedia o jogo de abrir. Agora é pulado e
  aplicado no próximo launch.

### Fast Flag Editor

- **Flags sumindo.** Com duas linhas de mesmo nome, as duas saíam das configurações salvas,
  inclusive a original. Agora só a cópia repetida fica marcada.
- **Mais leve.** Marcar caixas não salva mais a lista inteira a cada clique.

## 1.0.0 — Primeira versão

- Bootstrapper do Roblox com a cara do Claude desktop, estrelando o Clawd.
- Fluxo de atualização e launch obtido por engenharia reversa do Bloxstrap 2.11.4 com o REA e
  reescrito do zero em C#/WPF.
- Fast Flag Editor no estilo do Bloxstrap (Add single / Import JSON, validação, presets).
- Mods com backup e restauração dos arquivos originais.
- Instalação por usuário, sem precisar de administrador.
- Atualização automática pelo GitHub Releases.
