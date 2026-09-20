# Detecção de recompensas das raids

Após o clique Start Raid, `MacroRaids` e `MacroBossRaids` aguardam a tela de recompensa e clicam no centro do OK encontrado. Ambos agora retornam `Task` e devem ser chamados com `await`. A página Farming já foi atualizada.

## Critérios

- EMGU CV `MatchTemplate`, método `CcoeffNormed`, em tons de cinza com suavização leve para tolerar diferenças de antialiasing.
- Os dois botões devem atingir correlação mínima de 0,90 na mesma captura. Esse valor é similaridade, não probabilidade de acerto.
- Escala ajustada ao tamanho da área cliente, com busca adicional de ±15%, em passos de 2,5%.
- Busca no trecho inferior da janela, com verificação do alinhamento e espaçamento entre os botões.
- Três capturas consecutivas com posição estável antes do clique. Intervalo de 500 ms, além do tempo de processamento.
- Captura e clique em pixels da área cliente; barra de título e barra de tarefas não participam. O monitor considera DPI por monitor.
- O monitor alterna automaticamente o primeiro plano entre as janelas pendentes e restaura janelas minimizadas. Aguarda 350 ms após ativar uma janela antes de capturá-la; se a ativação falhar, não captura nem clica nela. Mantenha o desktop disponível para a automação, sem sobreposições.
- O clique verifica novamente a janela, suas dimensões e a janela sob o ponto encontrado.
- Após clicar, aguarda três capturas sem os botões. Se continuarem visíveis, tenta novamente após três segundos, até três cliques; falha e timeout interrompem o fluxo com uma mensagem.

## Fluxo e cancelamento

`RunRaidPairAsync` executa as duas raids concorrentemente. Cada janela prepara a sala, espera pelos jogadores e inicia sem aguardar o término da outra. A espera por jogadores libera o desktop para a outra janela. Um semáforo compartilhado serializa a preparação, o clique Start e cada captura/clique de recompensa, evitando que os dois fluxos disputem o mouse ou o foco.

Cada monitor mantém suas próprias confirmações, tentativas e timeout. A recompensa é fechada assim que confirmada naquela janela, mesmo se a outra raid continuar em andamento. A próxima rodada aguarda o encerramento das duas. Falha em uma cancela a outra tarefa de automação e ambas são aguardadas antes de liberar Start; isso não cancela a partida dentro do jogo.

A chamada `RunRaidPairAsync(mir42, mir40, cancellationToken: token)` está ativa na página Farming. Para Boss Raids, a mesma função aceita `boss: true`; a chamada foi mantida comentada conforme o estado atual da página.

A espera fixa de quatro minutos foi removida. A interface permanece responsiva, Start não inicia execuções duplicadas e Stop cancela as esperas das raids. Nas rotinas antigas de doação, economia de energia e missões, o cancelamento é verificado entre chamadas; essas rotinas ainda possuem esperas síncronas próprias.

## Imagens e ajustes

`Macro/Assets/RaidDetection/team-reward.png` e `ok.png` foram extraídos da captura fornecida, incluindo texto e contorno. São copiados para a saída e publicação. Idioma, tema e mudanças importantes de escala/layout do jogo podem exigir novos templates. Os recortes podem ser reproduzidos com `tools/Extract-RaidTemplates.ps1 -Source <captura original>`; as coordenadas desse script são específicas da imagem original.

O limiar fica em `RaidRewardDetector.MinimumConfidence`; tempo limite, intervalo e confirmações ficam em `RaidRewardMonitor`. A saída Debug registra similaridade e confirmações. Não reduza o limiar sem avaliar capturas reais positivas e negativas.

## Verificação

`dotnet run --project tests/RaidDetectionChecks -- <captura original>` executa testes sem capturar a tela ou movimentar o mouse. Foram verificados: referência, escalas de 75%, 66,7% e 125%, alteração de brilho, apenas OK, apenas informação, tela preta e botões com espaçamento incorreto. Os centros de clique também são conferidos.

Os testes usam a mesma imagem de origem dos templates e transformações sintéticas. Não medem a taxa de falsos positivos em partidas reais. Falta validar o ciclo completo no jogo, especialmente outras resoluções, animações, tempo de carregamento após OK e a tela de recompensa de Boss Raid. O fluxo mantém a espera existente de 1,5 segundo após fechar a recompensa; isso não confirma visualmente o fim do carregamento.

Referência da API: https://www.emgu.com/wiki/files/4.13.0/document/html/M_Emgu_CV_CvInvoke_MatchTemplate.htm
