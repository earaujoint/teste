using Emgu.CV.Dnn;
using Macro.Services.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Macro.Services
{
    /// <summary>
    /// Responsável por ações de mouse relativas à janela do jogo (clicks e captura de porcentagem do mouse).
    /// Mantém compatibilidade com a API estática existente e expõe sobrecargas que aceitam alvos configuráveis.
    /// </summary>
    public static class MovementService
    {
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        const int SW_MAXIMIZE = 3;
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        private static void ClickRelative(IntPtr hWnd, double xPercent, double yPercent, CancellationToken cancellationToken = default)
        {
            // Aceita valores em formato fractional (0.0 - 1.0) ou em porcentagem (ex: 56.79 -> 56.79%)
            if (xPercent > 1.0) xPercent /= 100.0;
            if (yPercent > 1.0) yPercent /= 100.0;

            GetClientRect(hWnd, out RECT rect);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            POINT point = new POINT
            {
                X = rect.Left + (int)(width * xPercent),
                Y = rect.Top + (int)(height * yPercent)
            };

            ClientToScreen(hWnd, ref point);

            SetCursorPos(point.X, point.Y);

            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(750);
        }
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        /// <summary>
        /// Calcula a posição do mouse como porcentagem relativa ao cliente da janela especificada e retorna o DTO MousePercentage.
        /// Também escreve os valores no Debug para compatibilidade com a funcionalidade existente.
        /// </summary>
        public static MousePercentage GetMousePercentage(IntPtr hWnd)
        {
            GetClientRect(hWnd, out RECT rect);

            GetCursorPos(out POINT mouse);

            POINT topLeft = new POINT
            {
                X = rect.Left,
                Y = rect.Top
            };

            ClientToScreen(hWnd, ref topLeft);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            double x = (double)(mouse.X - topLeft.X) / Math.Max(1, width);
            double y = (double)(mouse.Y - topLeft.Y) / Math.Max(1, height);

            Debug.WriteLine($"X: {x:P2}");
            Debug.WriteLine($"Y: {y:P2}");

            return new MousePercentage(x, y);
        }

        // Encontrar janela do processo e invocar GetMousePercentage uma vez
        /// <summary>
        /// Encontra processo alvo e registra uma amostra de porcentagem do mouse. Se target for nulo, usa Mir4G / Mir4G[2].
        /// </summary>
        public static MousePercentage? ShowMousePercentage(WindowTarget? target = null)
        {
            return MousePercentageMonitor.GetAtCursor();
        }

        // Loop contínuo para registrar a porcentagem do mouse em tempo real quando houver clique.
        /// <summary>
        /// Inicia observação de cliques e registra a porcentagem do mouse apenas quando um clique é detectado.
        /// Se target for nulo, usa Mir4G / Mir4G[2].
        /// </summary>
        public static void StartMousePercentageLoop(WindowTarget? target = null, int intervalMs = 200)
        {
            MousePercentageMonitor.Start();
        }

        public static void StopMousePercentageLoop()
        {
            MousePercentageMonitor.Stop();
        }

        private static void DragRelative(IntPtr hWnd, double startXPercent, double startYPercent, double endXPercent, double endYPercent, int durationMs = 500)
        {
            // Aceita 0.0 - 1.0 ou porcentagem, ex: 50.5
            if (startXPercent > 1.0) startXPercent /= 100.0;
            if (startYPercent > 1.0) startYPercent /= 100.0;
            if (endXPercent > 1.0) endXPercent /= 100.0;
            if (endYPercent > 1.0) endYPercent /= 100.0;

            GetClientRect(hWnd, out RECT rect);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // Ponto inicial
            POINT startPoint = new POINT
            {
                X = (int)(width * startXPercent),
                Y = (int)(height * startYPercent)
            };

            // Ponto final
            POINT endPoint = new POINT
            {
                X = (int)(width * endXPercent),
                Y = (int)(height * endYPercent)
            };

            // Converte coordenadas da janela para coordenadas da tela
            ClientToScreen(hWnd, ref startPoint);
            ClientToScreen(hWnd, ref endPoint);

            // Vai para o ponto inicial
            SetCursorPos(startPoint.X, startPoint.Y);

            Thread.Sleep(100);

            // Pressiona botão esquerdo
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);

            // Pequenas movimentações durante o arrasto
            int steps = Math.Max(1, durationMs / 10);

            for (int i = 1; i <= steps; i++)
            {
                double progress = (double)i / steps;

                int x = (int)(startPoint.X +
                              (endPoint.X - startPoint.X) * progress);

                int y = (int)(startPoint.Y +
                              (endPoint.Y - startPoint.Y) * progress);

                SetCursorPos(x, y);

                Thread.Sleep(5);
            }

            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(750);
        }
        private static void DragUpStrong(IntPtr hWnd)
        {
            GetClientRect(hWnd, out RECT rect);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            POINT start = new POINT
            {
                X = width / 2,
                Y = (int)(height * 0.90)
            };

            POINT end = new POINT
            {
                X = width / 2,
                Y = (int)(height * 0.30)
            };

            ClientToScreen(hWnd, ref start);
            ClientToScreen(hWnd, ref end);

            // Vai até o ponto inicial
            SetCursorPos(start.X, start.Y);

            Thread.Sleep(100);

            // SEGURA o botão esquerdo
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);

            // Arrasta para cima gradualmente
            int steps = 40;

            for (int i = 1; i <= steps; i++)
            {
                double progress = (double)i / steps;

                int x = start.X;
                int y = (int)(start.Y + (end.Y - start.Y) * progress);

                SetCursorPos(x, y);

                Thread.Sleep(15);
            }

            // SOLTA o botão esquerdo
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(300);
        }                          /// </summary>
        public static Process? FindProcess(WindowTarget target)
        {
            return Process.GetProcessesByName(target.ProcessName)
                .FirstOrDefault(p => p.MainWindowTitle == target.WindowTitle);
        }
        public static void RemoveEnergySave(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();

            //Drag relative
            input.Drag(30, 50, 70, 50, 1000);

            //Loot menu
            input.ClickRelative(55, 83);

            Thread.Sleep(1500);
        }
        /// <summary>
        /// Executa a sequência de cliques para "Macro Raids" no alvo informado. Se target for nulo, usa Mir4G / Mir4G[2].
        /// </summary>
        public static async Task MacroRaids(WindowTarget target, CancellationToken cancellationToken = default)
        {
            IntPtr handle = await StartRaidAsync(target, false, cancellationToken).ConfigureAwait(false);
            await RaidRewardMonitor.WaitAndDismissAsync(handle, cancellationToken).ConfigureAwait(false);
            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
        }

        public static async Task MacroBossRaids(WindowTarget target, CancellationToken cancellationToken = default)
        {
            IntPtr handle = await StartRaidAsync(target, true, cancellationToken).ConfigureAwait(false);
            await RaidRewardMonitor.WaitAndDismissAsync(handle, cancellationToken).ConfigureAwait(false);
            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static async Task FocusRaidAsync(IntPtr handle, CancellationToken token)
        {
            ShowWindow(handle, SW_MAXIMIZE);
            SetForegroundWindow(handle);
            await Task.Delay(350, token).ConfigureAwait(false);
            if (GetForegroundWindow() != handle)
                throw new InvalidOperationException("Não foi possível ativar a janela da raid.");
        }

        private static async Task<IntPtr> StartRaidAsync(WindowTarget target, bool boss, CancellationToken token)
        {
            var input = new InputService(target);
            IntPtr handle = input.Handle;
            await RaidWindowCoordinator.Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                input.Activate();
                input.ClickRelative(0.97, 0.04);
                input.ClickRelative(80.63, 57.5);
                input.ClickRelative(boss ? 80 : 72, 68);
                input.ClickRelative(86.51, 94.35);
                input.ClickRelative(49.90, 82.56);

            }
            finally { RaidWindowCoordinator.Gate.Release(); }

            // Waiting for players does not hold the desktop: the other raid can start/finish.
            await Task.Delay(boss ? 180000 : 60000, token).ConfigureAwait(false);
            await RaidWindowCoordinator.Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                input.Activate();
                // Start Raid
                input.ClickRelative(76.82, 85.83);
            }
            finally { RaidWindowCoordinator.Gate.Release(); }
            return handle;
        }

        public static async Task RunRaidPairAsync(WindowTarget first, WindowTarget second,
            bool boss = false, CancellationToken cancellationToken = default)
        {
            if (first == second) throw new ArgumentException("Selecione duas janelas diferentes.");
            using var pairCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            async Task RunOneAsync(WindowTarget target)
            {
                try
                {
                    if (!boss)
                    {
                        await RaidWindowCoordinator.Gate.WaitAsync(pairCts.Token).ConfigureAwait(false);
                        try
                        {
                            using var process = FindProcess(target)
                                ?? throw new InvalidOperationException($"Janela não encontrada: {target.WindowTitle}");
                            await FocusRaidAsync(process.MainWindowHandle, pairCts.Token).ConfigureAwait(false);
                            RemoveEnergySave(target);
                        }
                        finally { RaidWindowCoordinator.Gate.Release(); }
                    }

                    if (boss) await MacroBossRaids(target, pairCts.Token).ConfigureAwait(false);
                    else await MacroRaids(target, pairCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    pairCts.Cancel();
                    throw;
                }
            }
            // Observe both tasks; on a failure, cancel the sibling before leaving this round.
            await Task.WhenAll(RunOneAsync(first), RunOneAsync(second)).ConfigureAwait(false);
        }

        public static async Task RunBossRaidPairAsync(WindowTarget primary, WindowTarget secondary,
            CancellationToken cancellationToken = default)
        {
            if (primary == secondary) throw new ArgumentException("Selecione duas janelas diferentes.");

            var primaryInput = new InputService(primary);
            var secondaryInput = new InputService(secondary);

            await RaidWindowCoordinator.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ClickBossPrimary(primaryInput);
                AcceptBossInvite(secondaryInput);
                StartRaid(primaryInput);
            }
            finally
            {
                RaidWindowCoordinator.Gate.Release();
            }

            // Dá tempo para a tela da raid iniciar antes de o monitor alternar as janelas.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

            using var pairCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            async Task MonitorAsync(IntPtr handle)
            {
                try
                {
                    await RaidRewardMonitor.WaitAndDismissAsync(handle, pairCts.Token,
                        checkInterval: TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
                catch
                {
                    pairCts.Cancel();
                    throw;
                }
            }

            await Task.WhenAll(MonitorAsync(primaryInput.Handle), MonitorAsync(secondaryInput.Handle)).ConfigureAwait(false);
        }

        public static void ClickBossPrimary(InputService input)
        {
            input.Activate();
            void Click(int number, double x, double y)
            {
                Debug.WriteLine($"Boss primary: clique {number}/15 em X={x:F2}%, Y={y:F2}%");
                input.ClickRelative(x, y);
            }

            Click(1, 97.19, 5.25);
            Click(2, 80.57, 56.39);
            Click(3, 72.03, 69.08);
            Click(4, 85.78, 93.46);
            Click(5, 39.32, 56.89);

            Click(6, 70.05, 56.99);
            Click(7, 49.53, 59.17);
            Click(8, 49.53, 59.17);
            Click(9, 49.53, 59.17);
            Click(10, 49.53, 59.17);
            Click(11, 55.36, 84.44);
            Click(12, 55.31, 84.44);
            Click(13, 45.52, 39.05);
            Click(14, 56.30, 24.28);
            Click(15, 91.35, 15.86);
        }

        public static void AcceptBossInvite(InputService input)
        {
            input.Activate();
            void Click(double x, double y)
            {
                input.ClickRelative(x, y);
            }

            Click(17.40, 46.88);
            Click(56.35, 65.71);
        }

        public static void StartRaid(InputService input)
        {
            input.Activate();
            void Click(double x, double y)
            {
                input.ClickRelative(x, y);
            }
            Click(76.82, 85.83);
        }


        public static void DailyFavoriteMissions(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();
            void Click(double x, double y) => input.ClickRelative(x, y);

            //Menu Missions
            Click(85, 5);

            input.DragUp();
            Thread.Sleep(100);

            //Take 9 missions
            for (int i = 1; i < 10; i++)
            {
                Click(92.29, 88.80);
            }

            //Play auto button
            Click(78, 23);

            //Select all
            Click(16, 20);

            //Start missions
            Click(80, 86);

            Thread.Sleep(1500);
        }

        public static void DailyDonates(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();
            void Click(double x, double y) => input.ClickRelative(x, y);

            Click(80, 6);
            Click(69.58, 83.45);
            Click(47.92, 93.66);
            Click(67.55, 67.99);
            Click(66.87, 81.67);
            Click(56.25, 63.63);
            Click(30.89, 41.23);
            Click(67.19, 68.29);
            Click(66.15, 82.56);
            Click(56.41, 64.12);
            Click(30.52, 54.31);
            Click(67.66, 67.39);
            Click(67.50, 80.97);
            Click(56.09, 64.22);

            Thread.Sleep(1500);
        }

        public static void DomiMissions(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();
            void Click(double x, double y) => input.ClickRelative(x, y);

            Thread.Sleep(1000);

            // Open map - opens world/map view
            Click(87, 13);

            // Favorite map - select favorite map entry (first)
            Click(67, 22);

            // Favorite map - select favorite map entry (second/confirm)
            Click(67, 22);

            // Scroll or move map list (scroll bar)
            Click(43, 91);

            // Quit current mission / leave mission menu
            Click(57, 61);

            // Wait Fast travel
            Thread.Sleep(10000);

            // Open main menu
            Click(97, 3);

            // Transfer option in menu
            Click(88, 72);

            // Select Domi (entry 1)
            Click(88, 83);

            // Select Domi (entry 2)
            Click(86, 90);

            // Select Domi (entry 3 or confirm)
            Click(55, 69);

            //Wait for enter in domi
            Thread.Sleep(15000);

            // Open Missions tab
            Click(88, 4);

            // Accept mission
            Click(91, 37);

            // Start mission / start button
            Click(96, 36);

            //FastTravel
            FastTravel(target);

            // Fast travel / open map again to choose location
            Click(87, 13);

            // Choose location (first tap)
            Click(30, 39);

            // Choose location (second tap / confirm)
            Click(30, 36);

            // Wait 60 seconds for travel / loading
            Thread.Sleep(TimeSpan.FromSeconds(60));

            // Enter combat / start combat action
            Click(34, 91);

            // Wait 8 minutes for mission/combat to complete
            Thread.Sleep(TimeSpan.FromMinutes(8));

            // Repeat mission accept/start 1
            Click(88, 4); // Mission tab

            Click(91, 37); // Accept

            Click(96, 36); // Start

            Thread.Sleep(TimeSpan.FromMinutes(8)); // Wait another 8 minutes

            // Repeat mission accept/start 2
            Click(88, 4); // Mission tab

            Click(91, 37); // Accept
            Click(96, 36); // Start

            Thread.Sleep(TimeSpan.FromMinutes(8)); // Wait another 8 minutes

            // Exit / leave (Sair)
            Click(97, 20);

            // Confirm exit / confirmation dialog
            Click(56, 64);

        }

        public static void FastTravel(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();

            input.ClickRelative(75, 58);

            input.ClickRelative(54, 74);

            Thread.Sleep(10000);
        }

        public static void CreateArena(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();

            void Click(double x, double y) => input.ClickRelative(x, y);

            // Sequência de cliques atualizada pelo usuário
            Click(97.03, 4.96);
            Click(95.36, 57.09);
            Click(88.59, 74.43);
            Click(90.47, 93.26);
            Click(39.22, 57.09);
            Click(69.37, 56.89);
            Click(50.21, 60.06);
            Click(50.21, 59.96);
            Click(50.21, 59.96);
            Click(50.21, 59.96);
            Click(55.73, 85.63);
            Click(55.73, 85.63);
            Click(54.74, 51.34);
            Click(56.98, 24.38);
            Click(91.46, 15.56);

            Thread.Sleep(1000);

        }

        public static void DoArena(WindowTarget target1, WindowTarget target2,
            CancellationToken cancellationToken = default)
        {
            if (target1 == target2)
                throw new ArgumentException("Selecione duas janelas diferentes.");

            // Target2 cria a arena e envia o convite.
            CreateArena(target2);
            cancellationToken.ThrowIfCancellationRequested();

            // Target1 aceita o convite do boss.
            AcceptBossInvite(new InputService(target1));
            cancellationToken.ThrowIfCancellationRequested();

            // Volta ao Target2, aguarda a entrada na arena e então retorna ao Target1.
            new InputService(target2).Activate();
            Thread.Sleep(TimeSpan.FromSeconds(5));
            cancellationToken.ThrowIfCancellationRequested();

            new InputService(target1).Activate();
            Thread.Sleep(TimeSpan.FromSeconds(5));
            cancellationToken.ThrowIfCancellationRequested();

            // Clique final para iniciar a arena no Target1.
            StartRaid(new InputService(target1));
        }

    }
}
