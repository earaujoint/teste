# Modernização da interface Macro MIR4

## Implementado
- WPF/.NET 10 preservado, com navegação e instâncias das páginas existentes.
- Paleta escura/bege, sidebar, estilos compartilhados e templates de cards.
- Quatro abas internas de Farming, com cards que reorganizam em janelas menores.
- Seleção das duas janelas usadas pelas raids existentes; padrões preservados: Steam + Launcher 2.
- Ativação/repetição da raide normal e do boss atualmente implementados.
- Opções de doação e missões favoritas ligadas aos métodos existentes, na mesma ordem.
- Configuração salva ao iniciar em `%LOCALAPPDATA%/MacroMIR4/farming.json`.
- Log de execução, validação numérica e exclusão mútua entre Farming e Arena.
- Stop solicita cancelamento de Farming e Arena. Rotinas síncronas antigas terminam a chamada atual antes de atender à parada.
- Timer recebeu apenas ajustes de XAML. Horários, ordenação e janela de 30 minutos não foram modificados.

## Diferenças em relação às referências
O projeto não contém imagens ilustrativas dos bosses/itens, catálogo de mapas de missão, seleção de três raids normais/três boss raids, nem automação por item/mapa. As capturas de reconhecimento não foram usadas como arte de interface.

Os cards adicionais e os itens de missão estão desabilitados e identificados como pendentes. Não há MultiSelect funcional sem uma fonte de mapas e uma rotina capaz de executar essa seleção. Os models de missões diárias e Dominação têm configurações independentes, mas ainda não controlam o jogo. A rotina DomiMissions permanece fora de Start, como estava no código original. Limpeza de mochila permanece indisponível por não existir implementação. DailyDonates conserva sua lógica atual; não foi acrescentada uma regra de execução única por dia.

DoArena mantém os alvos originais (Launcher 1 + Steam), informados no tooltip. MousePercent e a área de captura continuam disponíveis.

## Verificação
`dotnet build Macro/MacroMir4.csproj --no-restore`

`dotnet run --project tests/UiChecks/UiChecks.csproj --no-restore`

Os testes carregam os recursos WPF sem mostrar janelas e verificam configurações, JSON, validação numérica, independência de missões, dimensões mínimas e as quatro abas em três larguras. Não executam automação do jogo.

A inspeção visual pelo Computer Use não ocorreu porque o pedido de acesso expirou. O teste antigo RaidDetectionChecks depende de uma captura original de recompensas, não fornecida neste pedido. O aviso CS8604 em BossService já existia antes desta alteração.
