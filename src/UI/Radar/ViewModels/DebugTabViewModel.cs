using LoneEftDmaRadar;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Tarkov.IL2CPP;
using System;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class DebugTabViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private string _DeviceAimbotDebugText = "DeviceAimbot Aimbot: (no data)";
        private bool _showDeviceAimbotDebug = App.Config.Device.ShowDebug;

        public DebugTabViewModel()
        {
            ToggleDebugConsoleCommand = new SimpleCommand(DebugLogger.Toggle);
            RunIl2CppDumperCommand = new SimpleCommand(RunIl2CppDumper);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) => RefreshDeviceAimbotDebug();
            _timer.Start();
            RefreshDeviceAimbotDebug();
        }

        public ICommand ToggleDebugConsoleCommand { get; }
        public ICommand RunIl2CppDumperCommand { get; }

        public bool ShowDeviceAimbotDebug
        {
            get => _showDeviceAimbotDebug;
            set
            {
                if (_showDeviceAimbotDebug == value)
                    return;
                _showDeviceAimbotDebug = value;
                App.Config.Device.ShowDebug = value;
                OnPropertyChanged(nameof(ShowDeviceAimbotDebug));
            }
        }

        public string DeviceAimbotDebugText
        {
            get => _DeviceAimbotDebugText;
            private set
            {
                if (_DeviceAimbotDebugText != value)
                {
                    _DeviceAimbotDebugText = value;
                    OnPropertyChanged(nameof(DeviceAimbotDebugText));
                }
            }
        }

        private void RunIl2CppDumper()
        {
            try
            {
                // Run the quick test - it will output to DebugLogger
                DumperExample.QuickTest();
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "RunIl2CppDumper");
            }
        }

        private void RefreshDeviceAimbotDebug()
        {
            var snapshot = MemDMA.DeviceAimbot?.GetDebugSnapshot();
            if (snapshot == null)
            {
                DeviceAimbotDebugText = "DeviceAimbot Aimbot: not running or no data yet.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== DeviceAimbot Aimbot ===");
            sb.AppendLine($"Status: {snapshot.Status}");
            sb.AppendLine($"Key: {(snapshot.KeyEngaged ? "ENGAGED" : "Idle")} | Enabled: {snapshot.Enabled} | Device: {(snapshot.DeviceConnected ? "Connected" : "Disconnected")}");
            sb.AppendLine($"InRaid: {snapshot.InRaid} | FOV: {snapshot.ConfigFov:F0}px | MaxDist: {snapshot.ConfigMaxDistance:F0}m | Mode: {snapshot.TargetingMode}");
            sb.AppendLine($"Filters -> PMC:{App.Config.Device.TargetPMC} PScav:{App.Config.Device.TargetPlayerScav} AI:{App.Config.Device.TargetAIScav} Boss:{App.Config.Device.TargetBoss} Raider:{App.Config.Device.TargetRaider}");
            sb.AppendLine($"Candidates: total {snapshot.CandidateTotal}, type {snapshot.CandidateTypeOk}, dist {snapshot.CandidateInDistance}, skeleton {snapshot.CandidateWithSkeleton}, w2s {snapshot.CandidateW2S}, final {snapshot.CandidateCount}");
            sb.AppendLine($"Target: {(snapshot.LockedTargetName ?? "None")} [{snapshot.LockedTargetType?.ToString() ?? "-"}] valid={snapshot.TargetValid}");
            if (snapshot.LockedTargetDistance.HasValue)
                sb.AppendLine($"  Dist {snapshot.LockedTargetDistance.Value:F1}m | FOVDist {(float.IsNaN(snapshot.LockedTargetFov) ? "n/a" : snapshot.LockedTargetFov.ToString("F1"))} | Bone {snapshot.TargetBone}");
            sb.AppendLine($"Fireport: {(snapshot.HasFireport ? snapshot.FireportPosition?.ToString() : "None")}");
            var bulletSpeedText = snapshot.BulletSpeed.HasValue ? snapshot.BulletSpeed.Value.ToString("F1") : "?";
            sb.AppendLine($"Ballistics: {(snapshot.BallisticsValid ? $"OK (Speed {bulletSpeedText} m/s, Predict {(snapshot.PredictionEnabled ? "ON" : "OFF")})" : "Invalid/None")}");

            DeviceAimbotDebugText = sb.ToString();
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
