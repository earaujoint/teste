using Macro.Models;
//using Macro.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Macro.Views
{
    public partial class TimerPage : Page
    {
        private readonly DispatcherTimer _timer;

        private List<Boss> _bosses = new();

        private readonly ObservableCollection<Boss> _availableBosses = new();

        private readonly HashSet<string> _processedSpawns = new();

        private readonly Dictionary<string, (Boss Boss, DateTime Expiration)> _availableSpawns = new();

        private static readonly TimeSpan AvailableDuration =
            TimeSpan.FromMinutes(30);

        private DateTime _lastCheck = DateTime.Now;

        public TimerPage()
        {
            InitializeComponent();

            AvailableBossesItemsControl.ItemsSource = _availableBosses;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timer.Tick += Timer_Tick;
            _timer.Start();

            _ = GetBosses();
        }

        private async Task GetBosses()
        {
            _bosses = await BossService.LoadBossesAsync();

            var now = DateTime.Now;

            // Ordenação inicial
            RefreshBosses();

            // Detecta bosses que já nasceram nos últimos 30 minutos
            CheckRecentSpawns(now);

            RefreshAvailablePanel();

            _lastCheck = now;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_bosses.Count == 0)
                return;

            var now = DateTime.Now;

            // Detecta novos spawns desde o último tick
            bool bossSpawned = CheckBossSpawns(_lastCheck, now);

            // Remove cards que completaram 30 minutos
            RemoveExpiredBosses(now);

            _lastCheck = now;

            // Apenas atualiza o texto dos tempos
            BossDataGrid.Items.Refresh();

            // Só reorganiza quando um novo boss nasceu
            if (bossSpawned)
            {
                RefreshBosses();
            }
        }

        private void CheckRecentSpawns(DateTime now)
        {
            var start = now - AvailableDuration;

            foreach (var boss in _bosses)
            {
                if (boss.SpawnTimes == null ||
                    boss.SpawnTimes.Count == 0)
                {
                    continue;
                }

                foreach (var spawnTime in boss.SpawnTimes)
                {
                    var spawnDateTime = now.Date.Add(spawnTime);

                    // Se o horário ainda não aconteceu hoje,
                    // verifica o mesmo horário do dia anterior.
                    if (spawnDateTime > now)
                    {
                        spawnDateTime = spawnDateTime.AddDays(-1);
                    }

                    // O spawn aconteceu dentro dos últimos 30 minutos
                    if (spawnDateTime >= start &&
                        spawnDateTime <= now)
                    {
                        AddAvailableBoss(boss, spawnDateTime);
                    }
                }
            }
        }

        private bool CheckBossSpawns(DateTime lastCheck, DateTime now)
        {
            bool bossSpawned = false;

            foreach (var boss in _bosses)
            {
                if (boss.SpawnTimes == null ||
                    boss.SpawnTimes.Count == 0)
                {
                    continue;
                }

                foreach (var spawnTime in boss.SpawnTimes)
                {
                    var spawnDateTime = now.Date.Add(spawnTime);

                    var spawnKey =
                        $"{boss.Name}_{boss.World}_{spawnDateTime:yyyyMMddHHmm}";

                    if (spawnDateTime > lastCheck &&
                        spawnDateTime <= now)
                    {
                        if (_processedSpawns.Contains(spawnKey))
                            continue;

                        _processedSpawns.Add(spawnKey);

                        AddAvailableBoss(boss, spawnDateTime);

                        bossSpawned = true;
                    }
                }
            }

            return bossSpawned;
        }

        private void AddAvailableBoss(Boss boss, DateTime spawnDateTime)
        {
            var spawnKey =
                $"{boss.Name}_{boss.World}_{spawnDateTime:yyyyMMddHHmm}";

            // Esse spawn já foi adicionado
            if (_availableSpawns.ContainsKey(spawnKey))
                return;

            var expirationTime =
                spawnDateTime + AvailableDuration;

            // Já passou dos 30 minutos
            if (expirationTime <= DateTime.Now)
                return;

            // Registra o spawn e sua expiração
            _availableSpawns[spawnKey] =
                (boss, expirationTime);

            _processedSpawns.Add(spawnKey);

            // Adiciona o boss visualmente apenas uma vez
            if (!_availableBosses.Contains(boss))
            {
                _availableBosses.Add(boss);
            }

            AvailablePanel.Visibility = Visibility.Visible;
        }

        private void RemoveExpiredBosses(DateTime now)
        {
            var expiredSpawns = _availableSpawns
                .Where(x => x.Value.Expiration <= now)
                .ToList();

            foreach (var item in expiredSpawns)
            {
                _availableSpawns.Remove(item.Key);

                var boss = item.Value.Boss;

                // Verifica se esse mesmo boss possui
                // outra ocorrência ainda disponível
                bool stillAvailable = _availableSpawns.Values
                    .Any(x =>
                        x.Boss.Name == boss.Name &&
                        x.Boss.World == boss.World);

                if (!stillAvailable)
                {
                    _availableBosses.Remove(boss);
                }
            }

            RefreshAvailablePanel();
        }

        private void RefreshAvailablePanel()
        {
            AvailablePanel.Visibility =
                _availableBosses.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void RefreshBosses()
        {
            _bosses = _bosses
                .OrderBy(b => b.NextSpawnTime)
                .ThenBy(b => b.World)
                .ToList();

            BossDataGrid.ItemsSource = _bosses;
        }
    }
}