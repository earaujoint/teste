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

        private static void ClickRelative(IntPtr hWnd, double xPercent, double yPercent)
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
            Thread.Sleep(500);
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
            var t = target ?? new WindowTarget("Mir4G", "Mir4G[2]");
            var process = FindProcess(t);

            if (process == null)
                return null;

            return GetMousePercentage(process.MainWindowHandle);
        }

        // Loop contínuo para registrar a porcentagem do mouse em tempo real quando houver clique.
        private static CancellationTokenSource? _mousePercentCts;

        /// <summary>
        /// Inicia observação de cliques e registra a porcentagem do mouse apenas quando um clique é detectado.
        /// Se target for nulo, usa Mir4G / Mir4G[2].
        /// </summary>
        public static void StartMousePercentageLoop(WindowTarget? target = null, int intervalMs = 200)
        {
            if (_mousePercentCts != null && !_mousePercentCts.IsCancellationRequested)
                return; // já em execução

            var t = target ?? new WindowTarget("Mir4G", "Mir4G[2]");
            var process = FindProcess(t);

            if (process == null)
                return;

            IntPtr handle = process.MainWindowHandle;

            _mousePercentCts = new CancellationTokenSource();
            var ct = _mousePercentCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    bool prevDown = false;
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            // VK_LBUTTON = 0x01
                            bool isDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
                            if (isDown && !prevDown)
                            {
                                // clique detectado: registra porcentagem
                                GetMousePercentage(handle);
                            }
                            prevDown = isDown;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Erro em GetMousePercentage: {ex.Message}");
                        }

                        await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // esperado ao cancelar
                }
            }, ct);
        }

        public static void StopMousePercentageLoop()
        {
            try
            {
                _mousePercentCts?.Cancel();
                _mousePercentCts?.Dispose();
            }
            finally
            {
                _mousePercentCts = null;
            }
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
            var process = FindProcess(target);

            if (process == null)
                return;

            IntPtr handle = process.MainWindowHandle;
            //Drag relative
            DragRelative(handle, 30, 50, 70, 50, 1000);

            //Loot menu
            ClickRelative(handle, 55, 83);
        }
        /// <summary>
        /// Executa a sequência de cliques para "Macro Raids" no alvo informado. Se target for nulo, usa Mir4G / Mir4G[2].
        /// </summary>
        public static void MacroRaids(WindowTarget target)
        {
            var process = FindProcess(target);

            if (process == null)
                return;

            IntPtr handle = process.MainWindowHandle;
            SetForegroundWindow(handle);
            ShowWindow(handle, SW_MAXIMIZE);

            //Click menu
            ClickRelative(handle, 0.97, 0.04);

            //Click raid button
            ClickRelative(handle, 80.63, 57.5);

            //Click raid button 2 
            ClickRelative(handle, 72, 68);

            //Create Raid
            ClickRelative(handle, 86.51, 94.35);

            //Create Raid 2
            ClickRelative(handle, 49.90, 82.56);

            //Wait for 17 seconds to ensure the raid is FILLED
            Thread.Sleep(17000);
            //Start Raid
            ClickRelative(handle, 76.82, 85.83);
        }

        public static void MacroBossRaids(WindowTarget target)
        {

            var process = FindProcess(target);

            if (process == null)
                return;

            IntPtr handle = process.MainWindowHandle;
            SetForegroundWindow(handle);
            ShowWindow(handle, SW_MAXIMIZE);

            //Click menu
            ClickRelative(handle, 0.97, 0.04);

            //Click raid BOSS button
            ClickRelative(handle, 80.63, 57.5);

            //Click raid button 2 
            ClickRelative(handle, 80, 68);

            //Create Raid
            ClickRelative(handle, 86.51, 94.35);

            //Create Raid 2
            ClickRelative(handle, 49.90, 82.56);

            //Wait for 3 minutes to ensure the raid is FILLED
            Thread.Sleep(1000 * 180);
            //Start Raid
            ClickRelative(handle, 76.82, 85.83);
        }

        public static void DailyFavoriteMissions(WindowTarget target)
        {
            var process = FindProcess(target);

            if (process == null)
                return;

            IntPtr handle = process.MainWindowHandle;
            SetForegroundWindow(handle);
            ShowWindow(handle, SW_MAXIMIZE);

            //Menu Missions
            ClickRelative(handle, 85, 5);

            DragUpStrong(handle);
            Thread.Sleep(100);

            DragUpStrong(handle);
            Thread.Sleep(100);

            DragUpStrong(handle);
            Thread.Sleep(100);

            DragUpStrong(handle);

            //Take 9 missions
            for (int i = 1; i < 10; i++)
            {
                ClickRelative(handle, 92.29, 88.80);
            }

            //Play auto button
            ClickRelative(handle, 78, 23);

            //Select all
            ClickRelative(handle, 16, 20);

            //Start missions
            ClickRelative(handle, 80, 86);
        }

        public static void DailyDonates(WindowTarget target)
        {
            var process = FindProcess(target);
            if (process == null)
                return;
            IntPtr handle = process.MainWindowHandle;
            SetForegroundWindow(handle);
            ShowWindow(handle, SW_MAXIMIZE);

            ClickRelative(handle, 80.21, 4.76);
            ClickRelative(handle, 69.58, 83.45);
            ClickRelative(handle, 47.92, 93.66);
            ClickRelative(handle, 67.55, 67.99);
            ClickRelative(handle, 66.87, 81.67);
            ClickRelative(handle, 56.25, 63.63);
            ClickRelative(handle, 30.89, 41.23);
            ClickRelative(handle, 67.19, 68.29);
            ClickRelative(handle, 66.15, 82.56);
            ClickRelative(handle, 56.41, 64.12);
            ClickRelative(handle, 30.52, 54.31);
            ClickRelative(handle, 67.66, 67.39);
            ClickRelative(handle, 67.50, 80.97);
            ClickRelative(handle, 56.09, 64.22);
        }
    }
}
