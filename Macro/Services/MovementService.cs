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
using System.Drawing;
using System.Drawing.Imaging;
using Macro.Utils;

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

                Thread.Sleep(4);
            }

            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(1000);
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
            input.Drag(30, 50, 70, 50);

      
            //Loot menu
            input.ClickRelative(55, 83);

            Thread.Sleep(1500);
        }

        /// <summary>Wakes the game only when its EnergySave screen is visible.</summary>
        public static void EnsureEnergySaveRemoved(WindowTarget target)
        {
            var input = new InputService(target);
            input.Activate();
            input.SendEscape();
            input.SendEscape();
            GetClientRect(input.Handle, out RECT client);
            var origin = new POINT();
            if (!ClientToScreen(input.Handle, ref origin) || client.Right <= 0 || client.Bottom <= 0) return;

            using var screenshot = new Bitmap(client.Right, client.Bottom, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(screenshot))
                graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, screenshot.Size);
            using var frame = ImagesUtils.BitmapToMat(screenshot);
            using var detector = new EnergySaveDetector();
            bool energySaveVisible = detector.Detect(frame, out double confidence);
            Debug.WriteLine($"EnergySave: confiança={confidence:F3}, detectado={energySaveVisible}");
            if (energySaveVisible) RemoveEnergySave(target);
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

        private static void ClickNormalPrimary(InputService input)
        {
            input.Activate();
            void Click(int number, double x, double y)
            {
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

            input.SendEscape();
        }

        public static void ClickRaidBossPrimary(InputService input)
        {
            input.Activate();
            void Click(double x, double y)
            {
                input.ClickRelative(x, y);
            }

            Click(96.98, 4.86);
            Click(80.99, 56.89);
            Click(80.63, 69.08);
            Click(85.36, 93.16);
            Click(50.36, 82.76);
            Click(45.99, 38.55);
            Click(56.93, 25.07);

            input.SendEscape();
        }

        public static Task DoNormalRaid(WindowTarget starter, WindowTarget inviter,
            CancellationToken cancellationToken = default) =>
            DoNormalRaid(starter, new[] { inviter }, cancellationToken);

        public static async Task DoNormalRaid(WindowTarget starter, IReadOnlyList<WindowTarget> inviters,
            CancellationToken cancellationToken = default)
        {
            var guests = inviters.ToArray();
            if (guests.Length > 2 || guests.Contains(starter) || guests.Distinct().Count() != guests.Length)
                throw new ArgumentException("Selecione até dois convidados diferentes do starter e entre si.");

            EnsureEnergySaveRemoved(starter);
            foreach (var guest in guests) EnsureEnergySaveRemoved(guest);

            var starterInput = new InputService(starter);
            var inviterInputs = guests.Select(guest => new InputService(guest)).ToArray();

            await RaidWindowCoordinator.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ClickNormalPrimary(starterInput);
                foreach (var input in inviterInputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AcceptBossInvite(input);
                }
                StartRaid(starterInput);
            }
            finally
            {
                RaidWindowCoordinator.Gate.Release();
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            starterInput.Activate();
            starterInput.SendCtrlOne();

            await WaitAndDismissRaidRewardsAsync(
                starterInput.Handle,
                inviterInputs.Select(input => input.Handle).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }

        public static Task DoBossRaid(WindowTarget starter, WindowTarget inviter,
            CancellationToken cancellationToken = default) =>
            DoBossRaid(starter, new[] { inviter }, cancellationToken);

        public static async Task DoBossRaid(WindowTarget starter, IReadOnlyList<WindowTarget> inviters,
            CancellationToken cancellationToken = default)
        {
            var guests = inviters.ToArray();
            if (guests.Length > 2 || guests.Contains(starter) || guests.Distinct().Count() != guests.Length)
                throw new ArgumentException("Selecione até dois convidados diferentes do starter e entre si.");

            EnsureEnergySaveRemoved(starter);
            foreach (var guest in guests) EnsureEnergySaveRemoved(guest);

            var starterInput = new InputService(starter);
            var inviterInputs = guests.Select(guest => new InputService(guest)).ToArray();

            await RaidWindowCoordinator.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ClickRaidBossPrimary(starterInput);
                foreach (var input in inviterInputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AcceptBossInvite(input);
                }
            }
            finally
            {
                RaidWindowCoordinator.Gate.Release();
            }

            // No boss, a detecção começa assim que os movimentos e o aceite terminam.
            // Não há espera fixa nem chamada de StartRaid neste fluxo.
            await WaitAndDismissRaidRewardsAsync(
                starterInput.Handle,
                inviterInputs.Select(input => input.Handle).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task WaitAndDismissRaidRewardsAsync(
            IntPtr starterHandle,
            IReadOnlyList<IntPtr> inviterHandle,
            CancellationToken cancellationToken)
        {
            using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                await RaidRewardMonitor.WaitAndDismissLinkedAsync(
                    starterHandle,
                    inviterHandle,
                    monitorCts.Token).ConfigureAwait(false);
            }
            catch
            {
                monitorCts.Cancel();
                throw;
            }
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
            EnsureEnergySaveRemoved(target);
            var input = new InputService(target);
            input.Activate();
            void Click(double x, double y) => input.ClickRelative(x, y);

            Thread.Sleep(500);

            //Menu Missions
            Click(85, 5);

            //Field
            Click(5.52, 13.48);

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
            Click(13.70, 21.51);

            //Start missions
            Click(80, 86);

            Thread.Sleep(1000);

            FastTravel(target);
        }

        public static void DailyDonates(WindowTarget target)
        {
            EnsureEnergySaveRemoved(target);
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

            input.SendEscape();
            input.SendEscape();
            input.SendEscape();

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

            Thread.Sleep(2000);
            Click(54.74, 51.34);
            Click(56.98, 24.38);
            input.SendEscape();
            Thread.Sleep(1000);
        }

        public static void DoArena(WindowTarget target1, WindowTarget target2,
            CancellationToken cancellationToken = default)
        {
            if (target1 == target2)
                throw new ArgumentException("Selecione duas janelas diferentes.");

            EnsureEnergySaveRemoved(target1);
            EnsureEnergySaveRemoved(target2);

            // Target2 cria a arena e envia o convite.
            CreateArena(target2);
            cancellationToken.ThrowIfCancellationRequested();

            // Target1 aceita o convite do boss.
            AcceptBossInvite(new InputService(target1));
            cancellationToken.ThrowIfCancellationRequested();

            // Volta ao Target2, aguarda a entrada na arena e então retorna ao Target1.
            new InputService(target2).Activate();
            Thread.Sleep(TimeSpan.FromSeconds(3));
            cancellationToken.ThrowIfCancellationRequested();

            var finalInput = new InputService(target2);
            finalInput.Activate();
            finalInput.ClickRelative(48.39, 90.88);

            Thread.Sleep(TimeSpan.FromSeconds(7));
            finalInput.ClickRelative(75.36, 20.22);

            finalInput.ClickRelative(55.05, 75.32);

            var input = new InputService(target1);
            input.Activate();
            Thread.Sleep(TimeSpan.FromSeconds(8));
            input.ClickRelative(48.39, 90.88);
            Thread.Sleep(TimeSpan.FromSeconds(5));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
