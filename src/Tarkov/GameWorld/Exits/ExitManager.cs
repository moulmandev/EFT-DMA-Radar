/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using static LoneEftDmaRadar.Tarkov.Unity.UnitySDK;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Exits
{
    /// <summary>
    /// List of PMC/Scav 'Exits' in Local Game World and their position/status.
    /// </summary>
    public sealed class ExitManager : IReadOnlyCollection<IExitPoint>
    {
        private IReadOnlyList<IExitPoint> _exits;

        private readonly ulong _localGameWorld;
        private readonly string _mapId;
        private readonly bool _isPMC;
        private ulong exfilArrayAddr;
        private ulong entryPointPtr;
        private string entryPointName;
        private readonly LocalPlayer _localPlayer;

        public ExitManager(ulong localGameWorld, string mapId, LocalPlayer localPlayer)
        {
            _localGameWorld = localGameWorld;
            _isPMC = localPlayer.IsPmc;
            _mapId = mapId;
            _localPlayer = localPlayer;
        }

        private void Init()
        {
            var list = new List<IExitPoint>();

            try
            {
                var exfiltrationController = Memory.ReadPtr(_localGameWorld + Offsets.GameWorld.ExfiltrationController);
                exfilArrayAddr = Memory.ReadPtr(exfiltrationController + (_isPMC ? 0x20u : 0x28u), false);
                if (_isPMC)
                {
                    entryPointPtr = Memory.ReadPtr(_localPlayer.Info + Offsets.PlayerInfo.EntryPoint);
                    entryPointName = Memory.ReadUnityString(entryPointPtr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExitManager] Init Error: {ex}");
                _exits = list;
                return;
            }

            using var exfilArray = UnityArray<ulong>.Create(exfilArrayAddr, false);
            foreach (var exfilAddr in exfilArray)
            {
                var namePtr = Memory.ReadPtrChain(exfilAddr, false, new[] { Offsets.ExfiltrationPoint.Settings, Offsets.ExitTriggerSettings.Name });
                var exfilName = Memory.ReadUnityString(namePtr)?.Trim();

                var transformInternal = Memory.ReadPtrChain(exfilAddr, false, UnityOffsets.TransformChain);
                var _position = new UnityTransform(transformInternal, false).UpdatePosition();
                if (_isPMC)
                {
                    ulong eligibleEntryPointsArray = Memory.ReadPtr(exfilAddr + Offsets.ExfiltrationPoint.EligibleEntryPoints, false);
                    using var eligibleEntryPoints = UnityArray<ulong>.Create(eligibleEntryPointsArray, false);
                    foreach (var eligibleEntryPointAddr in eligibleEntryPoints)
                    {
                        string entryPointIDStr = Memory.ReadUnityString(eligibleEntryPointAddr);
                        if (!string.IsNullOrEmpty(entryPointIDStr) && entryPointIDStr.Equals(entryPointName, StringComparison.OrdinalIgnoreCase))
                        {
                            var exfil = new Exfil(exfilAddr, exfilName, _mapId, _isPMC, _position);
                            list.Add(exfil);
                        }
                    }
                }
                else
                {
                    var eligibleIdsAddr = Memory.ReadPtr(exfilAddr + Offsets.ScavExfiltrationPoint.EligibleIds, false);
                    using var eligibleIdsList = UnityList<ulong>.Create(eligibleIdsAddr, false);
                    if (eligibleIdsList.Count > 0)
                    {
                        var exfil = new Exfil(exfilAddr, exfilName, _mapId, _isPMC, _position);
                        list.Add(exfil);
                    }

                }

            }
            if (TarkovDataManager.MapData.TryGetValue(_mapId, out var map))
            {
                var filteredExfils = _isPMC ?
                    map.Extracts.Where(x => x.IsShared || x.IsPmc) :
                    map.Extracts.Where(x => !x.IsPmc);
                foreach (var transit in map.Transits)
                {
                    list.Add(new TransitPoint(transit));
                }
            }

            _exits = list;
        }

        public void Refresh()
        {
            try
            {
                if (_exits is null) // Initialize
                    Init();
                ArgumentNullException.ThrowIfNull(_exits, nameof(_exits));
                var map = Memory.CreateScatterMap();
                var round1 = map.AddRound();

                for (int ix = 0; ix < _exits.Count; ix++)
                {
                    int i = ix;
                    var entry = _exits[i];
                    if (entry is Exfil exfil)
                    {
                        round1.PrepareReadValue<int>(exfil.exfilBase + Offsets.ExfiltrationPoint._status);
                        round1.Completed += (sender, s1) =>
                        {
                            if (s1.ReadValue<int>(exfil.exfilBase + Offsets.ExfiltrationPoint._status, out var exfilStatus))
                            {
                                exfil.Update((Enums.EExfiltrationStatus)exfilStatus);
                            }
                        };
                    }
                }
                map.Execute();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExitManager] Refresh Error: {ex}");
            }
        }

        #region IReadOnlyCollection

        public int Count => _exits?.Count ?? 0;
        public IEnumerator<IExitPoint> GetEnumerator() => _exits?.GetEnumerator() ?? Enumerable.Empty<IExitPoint>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
