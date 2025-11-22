using LoneEftDmaRadar.Common.Unity.Collections;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Common.DMA;
using LoneEftDmaRadar.Common.Unity;

namespace LoneEftDmaRadar.Tarkov.GameWorld
{


    public sealed class CameraManager
    {
        private static ulong _fpsCamera;
        private static ulong _opticCamera;
        private static bool _isInitialized;

        public float FOV { get; private set; } = 60f;
        public float AspectRatio { get; private set; } = 1.777f;
        public float ZoomLevel { get; private set; } = 1f;
        public Matrix4x4 ViewMatrix { get; private set; } = Matrix4x4.Identity;
        public bool IsADS { get; private set; }
        public bool IsScoped { get; private set; }

        /// <summary>
        /// Resets the camera manager state. Call this when leaving a raid to clean up stale data.
        /// </summary>
        public static void Reset()
        {
            _fpsCamera = 0;
            _opticCamera = 0;
            _isInitialized = false;
            DebugLogger.LogInfo("CameraManager: Reset - cleared all camera data");
        }

        public static bool TryInitialize()
        {
            if (_isInitialized)
                return true;

            try
            {
                DebugLogger.LogDebug("CameraManager: Starting initialization...");
                
                var addr = Memory.ReadPtr(Memory.UnityBase + UnitySDK.UnityOffsets.CameraObjectManager, false);
                if (addr == 0)
                {
                    DebugLogger.LogWarning("CameraManager: Failed to read CameraObjectManager address");
                    return false;
                }

                var cameraManager = Memory.ReadPtr(addr, false);
                if (cameraManager == 0)
                {
                    DebugLogger.LogWarning("CameraManager: Failed to read cameraManager pointer");
                    return false;
                }

                DebugLogger.LogDebug($"CameraManager: Searching for cameras at 0x{cameraManager:X}");
                int camerasFound = 0;

                for (int i = 0; i < 100; i++)
                {
                    var camera = Memory.ReadPtr(cameraManager + (ulong)i * 0x8, false);
                    if (camera == 0)
                        continue;

                    Span<uint> nameChain = stackalloc uint[] { 0x50, 0x80 };
                    var namePtr = Memory.ReadPtrChain(camera, false, nameChain);
                    
                    if (namePtr == 0)
                        continue;

                    string name;
                    try
                    {
                        name = Memory.ReadUtf8String(namePtr, 128, false);
                    }
                    catch
                    {
                        // Pointer occasionally goes stale between raids; skip this entry and keep scanning.
                        continue;
                    }
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        camerasFound++;
                        
                        if (name.Contains("Camera", StringComparison.OrdinalIgnoreCase) || 
                            name.Contains("Optic", StringComparison.OrdinalIgnoreCase))
                        {
                            DebugLogger.LogDebug($"CameraManager: Found GameObject[{i}] = '{name}' at 0x{camera:X}");
                        }
                        else if (camerasFound <= 10)
                        {
                            DebugLogger.LogDebug($"CameraManager: Found GameObject[{i}] = '{name}'");
                        }
                    }

                    if (name == "FPS Camera")
                    {
                        _fpsCamera = camera;
                        DebugLogger.LogInfo($"CameraManager: Found FPS Camera at 0x{camera:X}");
                    }
                    else if (name == "BaseOpticCamera(Clone)")
                    {
                        _opticCamera = camera;
                        DebugLogger.LogInfo($"CameraManager: Found BaseOpticCamera at 0x{camera:X}");
                    }

                if (_fpsCamera != 0 && _opticCamera != 0)
                {
                    _isInitialized = true;
                    DebugLogger.LogInfo($"CameraManager: Successfully initialized with BOTH cameras - FPS: 0x{_fpsCamera:X}, Optic: 0x{_opticCamera:X}");
                    return true;
                }
            }

            if (_fpsCamera != 0)
            {
                _isInitialized = true;
                DebugLogger.LogWarning($"CameraManager: Initialized with FPS Camera ONLY (0x{_fpsCamera:X}) - Optic Camera NOT FOUND! PiP scopes may not work correctly.");
                return true;
            }

            DebugLogger.LogWarning($"CameraManager: No cameras found (scanned {camerasFound} objects)");
            return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "CameraManager.TryInitialize");
                Reset();
                return false;
            }
        }


        public static bool OpticCameraActive => _opticCamera != 0;
        
         /// <summary>
        /// Checks if the Optic Camera is active and there is an active scope zoom level greater than 1.
        /// </summary>
        /// <returns>True if scoped in, otherwise False.</returns>
        private bool CheckIfScoped(Player.LocalPlayer localPlayer)
        {
            try
            {
                if (localPlayer is null)
                    return false;
                if (OpticCameraActive)
                {
                    var opticsPtr = Memory.ReadPtr(localPlayer.PWA + Offsets.ProceduralWeaponAnimation._optics);
                    if (opticsPtr == 0)
                        return false;

                    using var optics = MemList<MemPointer>.Get(opticsPtr);
                    if (optics.Count > 0)
                    {
                        var pSightComponent = Memory.ReadPtr(optics[0] + Offsets.SightNBone.Mod);
                        if (pSightComponent == 0)
                            return false;

                        var sightComponent = Memory.ReadValue<SightComponent>(pSightComponent);

                        if (sightComponent.ScopeZoomValue != 0f)
                            return sightComponent.ScopeZoomValue > 1f;
                        return sightComponent.GetZoomLevel() > 1f; // Make sure we're actually zoomed in
                    }
                }
                return false;
            }
            catch
            {
                // Silently fail - this is expected when switching weapons/sights rapidly
                return false;
            }
        }

        private static bool _lastADSState = false;
        private static int _updateCounter = 0;

        public void Update(Player.LocalPlayer localPlayer)
        {
            if (!_isInitialized || _fpsCamera == 0)
            {
                TryInitialize();
                return;
            }

            try
            {
                IsADS = localPlayer?.IsAiming ?? false;
                IsScoped = IsADS && CheckIfScoped(localPlayer);
                
                if (IsADS != _lastADSState)
                {
                    DebugLogger.LogInfo($"CameraManager: ADS State Changed - IsADS={IsADS}, IsScoped={IsScoped}, OpticCamera={(_opticCamera != 0 ? "Available" : "NOT FOUND")}");
                    _lastADSState = IsADS;
                }

                // Determine which camera to use for ViewMatrix and FOV
                bool useOpticCamera = IsADS && IsScoped && _opticCamera != 0;

                if (useOpticCamera)
                {
                    // When scoped with PiP optic, use optic camera's ViewMatrix/FOV/Aspect/Zoom
                    ViewMatrix = Memory.ReadValue<Matrix4x4>(_opticCamera + UnitySDK.UnityOffsets.Camera.ViewMatrix, false);
                    FOV = Memory.ReadValue<float>(_opticCamera + UnitySDK.UnityOffsets.Camera.FOV, false);
                    AspectRatio = Memory.ReadValue<float>(_opticCamera + UnitySDK.UnityOffsets.Camera.AspectRatio, false);
                    ZoomLevel = Memory.ReadValue<float>(_opticCamera + UnitySDK.UnityOffsets.Camera.ZoomLevel, false);

                    _updateCounter++;
                    if (_updateCounter % 300 == 0)
                    {
                        float fpsFov = Memory.ReadValue<float>(_fpsCamera + UnitySDK.UnityOffsets.Camera.FOV, false);
                        DebugLogger.LogDebug($"CameraManager: SCOPED (PiP) - ViewMatrix/FOV from Optic, FPS_FOV={fpsFov:F2}, AspectRatio={AspectRatio:F3}, Zoom={ZoomLevel:F2}x");
                    }
                }
                else
                {
                    // Regular FPS camera for hipfire and iron sights
                    ViewMatrix = Memory.ReadValue<Matrix4x4>(_fpsCamera + UnitySDK.UnityOffsets.Camera.ViewMatrix, false);
                    FOV = Memory.ReadValue<float>(_fpsCamera + UnitySDK.UnityOffsets.Camera.FOV, false);
                    AspectRatio = Memory.ReadValue<float>(_fpsCamera + UnitySDK.UnityOffsets.Camera.AspectRatio, false);
                    ZoomLevel = Memory.ReadValue<float>(_fpsCamera + UnitySDK.UnityOffsets.Camera.ZoomLevel, false);

                    _updateCounter++;
                    if (_updateCounter % 300 == 0)
                    {
                        DebugLogger.LogDebug($"CameraManager: Using FPS Camera - IsADS={IsADS}, IsScoped={IsScoped}, FOV={FOV:F2}, AspectRatio={AspectRatio:F3}, Zoom={ZoomLevel:F2}x");
                    }
                }

                if (FOV < 1f || FOV > 180f)
                {
                    DebugLogger.LogWarning($"CameraManager: Invalid FOV {FOV:F2}, resetting to 60");
                    FOV = 60f;
                }

                if (AspectRatio < 0.5f || AspectRatio > 5f)
                {
                    DebugLogger.LogWarning($"CameraManager: Invalid AspectRatio {AspectRatio:F3}, resetting to 16:9 (1.778)");
                    AspectRatio = 1.777778f;
                }

                if (ZoomLevel < 0.1f || ZoomLevel > 100f)
                {
                    ZoomLevel = 1f;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "CameraManager.Update");
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private readonly ref struct SightComponent // (Type: EFT.InventoryLogic.SightComponent)
        {
            [FieldOffset((int)Offsets.SightComponent._template)] private readonly ulong pSightInterface;
            [FieldOffset((int)Offsets.SightComponent.ScopesSelectedModes)] private readonly ulong pScopeSelectedModes;
            [FieldOffset((int)Offsets.SightComponent.SelectedScope)] private readonly int SelectedScope;
            [FieldOffset((int)Offsets.SightComponent.ScopeZoomValue)] public readonly float ScopeZoomValue;

            public readonly float GetZoomLevel()
            {
                using var zoomArray = SightInterface.Zooms;

                if (SelectedScope >= zoomArray.Count || SelectedScope is < 0 or > 10)
                    return -1.0f;

                using var selectedScopeModes = MemArray<int>.Get(pScopeSelectedModes, false);
                int selectedScopeMode = SelectedScope >= selectedScopeModes.Count ? 0 : selectedScopeModes[SelectedScope];
                ulong zoomAddr = zoomArray[SelectedScope] + MemArray<float>.ArrBaseOffset + (uint)selectedScopeMode * 0x4;

                float zoomLevel = Memory.ReadValue<float>(zoomAddr, false);

                if (zoomLevel.IsNormalOrZero() && zoomLevel is >= 0f and < 100f)
                    return zoomLevel;

                return -1.0f;
            }

            public readonly SightInterface SightInterface => Memory.ReadValue<SightInterface>(pSightInterface);
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private readonly ref struct SightInterface // _template (Type: -.GInterfaceBB26)

        {
            [FieldOffset((int)Offsets.SightInterface.Zooms)] private readonly ulong pZooms;

            public readonly MemArray<ulong> Zooms =>
                MemArray<ulong>.Get(pZooms);
        }
    }
}
