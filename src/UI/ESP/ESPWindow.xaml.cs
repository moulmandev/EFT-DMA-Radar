using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using System.Drawing;
using System.Linq;
using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using LoneEftDmaRadar.UI.Skia;
using LoneEftDmaRadar.UI.Misc;
using SharpDX;
using SharpDX.Mathematics.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms.Integration;
using WinForms = System.Windows.Forms;
using DxColor = SharpDX.Mathematics.Interop.RawColorBGRA;

namespace LoneEftDmaRadar.UI.ESP
{
    public partial class ESPWindow : Window
    {
        #region Fields/Properties

        public static bool ShowESP { get; set; } = true;
        private bool _dxInitFailed;

        private readonly System.Diagnostics.Stopwatch _fpsSw = new();
        private int _fpsCounter;
        private int _fps;
        private long _lastFrameTicks;
        private Timer _highFrequencyTimer;
        private int _renderPending;

        // Render surface
        private Dx9OverlayControl _dxOverlay;
        private WindowsFormsHost _dxHost;
        private bool _isClosing;

        // Cached Fonts/Paints
        private readonly SKPaint _skeletonPaint;
        private readonly SKPaint _boxPaint;
        private readonly SKPaint _lootPaint;
        private readonly SKPaint _lootTextPaint;
        private readonly SKPaint _crosshairPaint;
        private static readonly SKColor[] _espGroupPalette = new SKColor[]
        {
            SKColors.MediumSlateBlue,
            SKColors.MediumSpringGreen,
            SKColors.CadetBlue,
            SKColors.MediumOrchid,
            SKColors.PaleVioletRed,
            SKColors.SteelBlue,
            SKColors.DarkSeaGreen,
            SKColors.Chocolate
        };
        private static readonly ConcurrentDictionary<int, SKPaint> _espGroupPaints = new();

        private Vector3 _camPos;
        private bool _isFullscreen;
        private readonly CameraManager _cameraManager = new();

        /// <summary>
        /// LocalPlayer (who is running Radar) 'Player' object.
        /// </summary>
        private static LocalPlayer LocalPlayer => Memory.LocalPlayer;

        /// <summary>
        /// All Players in Local Game World (including dead/exfil'd) 'Player' collection.
        /// </summary>
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => Memory.Players;

        private static IReadOnlyCollection<IExitPoint> Exits => Memory.Exits;

        private static bool InRaid => Memory.InRaid;

        // Bone Connections for Skeleton
        private static readonly (Bones From, Bones To)[] _boneConnections = new[]
        {
            (Bones.HumanHead, Bones.HumanNeck),
            (Bones.HumanNeck, Bones.HumanSpine3),
            (Bones.HumanSpine3, Bones.HumanSpine2),
            (Bones.HumanSpine2, Bones.HumanSpine1),
            (Bones.HumanSpine1, Bones.HumanPelvis),
            
            // Left Arm
            (Bones.HumanNeck, Bones.HumanLUpperarm), // Shoulder approx
            (Bones.HumanLUpperarm, Bones.HumanLForearm1),
            (Bones.HumanLForearm1, Bones.HumanLForearm2),
            (Bones.HumanLForearm2, Bones.HumanLPalm),
            
            // Right Arm
            (Bones.HumanNeck, Bones.HumanRUpperarm), // Shoulder approx
            (Bones.HumanRUpperarm, Bones.HumanRForearm1),
            (Bones.HumanRForearm1, Bones.HumanRForearm2),
            (Bones.HumanRForearm2, Bones.HumanRPalm),
            
            // Left Leg
            (Bones.HumanPelvis, Bones.HumanLThigh1),
            (Bones.HumanLThigh1, Bones.HumanLThigh2),
            (Bones.HumanLThigh2, Bones.HumanLCalf),
            (Bones.HumanLCalf, Bones.HumanLFoot),
            
            // Right Leg
            (Bones.HumanPelvis, Bones.HumanRThigh1),
            (Bones.HumanRThigh1, Bones.HumanRThigh2),
            (Bones.HumanRThigh2, Bones.HumanRCalf),
            (Bones.HumanRCalf, Bones.HumanRFoot),
        };

        #endregion

        public ESPWindow()
        {
            InitializeComponent();
            CameraManager.TryInitialize();
            InitializeRenderSurface();
            
            // Initial sizes
            this.Width = 400;
            this.Height = 300;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Cache paints/fonts
            _skeletonPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            _boxPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.0f,
                IsAntialias = false, // Crisper boxes
                Style = SKPaintStyle.Stroke
            };

            _lootPaint = new SKPaint
            {
                Color = SKColors.LightGray,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

             _lootTextPaint = new SKPaint
            {
                Color = SKColors.Silver,
                Style = SKPaintStyle.Fill
            };

            _crosshairPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            _fpsSw.Start();
            _lastFrameTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            _highFrequencyTimer = new System.Threading.Timer(
                callback: HighFrequencyRenderCallback,
                state: null,
                dueTime: 0,
                period: 1); // push frames as fast as dispatcher allows
        }

        private void InitializeRenderSurface()
        {
            RenderRoot.Children.Clear();

            _dxOverlay = new Dx9OverlayControl
            {
                Dock = WinForms.DockStyle.Fill
            };

            ApplyDxFontConfig();
            _dxOverlay.RenderFrame = RenderSurface;
            _dxOverlay.DeviceInitFailed += Overlay_DeviceInitFailed;
            _dxOverlay.MouseDown += GlControl_MouseDown;
            _dxOverlay.DoubleClick += GlControl_DoubleClick;
            _dxOverlay.KeyDown += GlControl_KeyDown;

            _dxHost = new WindowsFormsHost
            {
                Child = _dxOverlay
            };

            RenderRoot.Children.Add(_dxHost);
        }

        private void HighFrequencyRenderCallback(object state)
        {
            try
            {
                if (_isClosing)
                    return;

                int maxFPS = App.Config.UI.EspMaxFPS;
                long currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                if (maxFPS > 0)
                {
                    double elapsedMs = (currentTicks - _lastFrameTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    double targetMs = 1000.0 / maxFPS;
                    if (elapsedMs < targetMs)
                        return;
                }

                _lastFrameTicks = currentTicks;

                // Must dispatch to UI thread for rendering; avoid piling up work
                if (System.Threading.Interlocked.CompareExchange(ref _renderPending, 1, 0) == 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RefreshESP();
                    }), System.Windows.Threading.DispatcherPriority.Send);
                }
            }
            catch { /* Ignore errors during shutdown */ }
        }

        #region Rendering Methods

        /// <summary>
        /// Record the Rendering FPS.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SetFPS()
        {
            if (_fpsSw.ElapsedMilliseconds >= 1000)
            {
                _fps = System.Threading.Interlocked.Exchange(ref _fpsCounter, 0);
                _fpsSw.Restart();
            }
            else
            {
                _fpsCounter++;
            }
        }

        private bool _lastInRaidState = false;

        /// <summary>
        /// Main ESP Render Event.
        /// </summary>
        private void RenderSurface(Dx9RenderContext ctx)
        {
            if (_dxInitFailed)
                return;

            float screenWidth = ctx.Width;
            float screenHeight = ctx.Height;

            SetFPS();

            // Clear with black background (transparent for fuser)
            ctx.Clear(new DxColor(0, 0, 0, 255));

            try
            {
                // Detect raid state changes and reset camera/state when leaving raid
                if (_lastInRaidState && !InRaid)
                {
                    CameraManager.Reset();
                    _transposedViewMatrix = new TransposedViewMatrix();
                    _camPos = Vector3.Zero;
                    DebugLogger.LogInfo("ESP: Detected raid end - reset all state");
                }
                _lastInRaidState = InRaid;

                if (!InRaid)
                    return;

                var localPlayer = LocalPlayer;
                var allPlayers = AllPlayers;
                
                if (localPlayer is not null && allPlayers is not null)
                {
                    if (!ShowESP)
                    {
                        DrawNotShown(ctx, screenWidth, screenHeight);
                    }
                    else
                    {
                        _cameraManager.Update(localPlayer);
                        UpdateCameraPositionFromMatrix();

                        ApplyResolutionOverrideIfNeeded();

                        // Render Loot (background layer)
                        if (App.Config.Loot.Enabled && App.Config.UI.EspLoot)
                        {
                            DrawLoot(ctx, screenWidth, screenHeight);
                        }

                        // Render Exfils
                        if (Exits is not null && App.Config.UI.EspExfils)
                        {
                            foreach (var exit in Exits)
                            {
                                if (exit is Exfil exfil && exfil.Status != Exfil.EStatus.Closed)
                                {
                                     if (WorldToScreen2(exfil.Position, out var screen, screenWidth, screenHeight))
                                     {
                                         var paint = exfil.Status switch
                                         {
                                             Exfil.EStatus.Open => SKPaints.PaintExfilOpen,
                                             Exfil.EStatus.Pending => SKPaints.PaintExfilPending,
                                             _ => SKPaints.PaintExfilOpen
                                         };
                                         
                                         ctx.DrawCircle(ToRaw(screen), 4f, ToColor(paint), true);
                                         ctx.DrawText(exfil.Name, screen.X + 6, screen.Y + 4, ToColor(SKPaints.TextExfil), DxTextSize.Medium);
                                     }
                                }
                            }
                        }

                        // Render players
                        foreach (var player in allPlayers)
                        {
                            DrawPlayerESP(ctx, player, localPlayer, screenWidth, screenHeight);
                        }

                        if (App.Config.UI.EspCrosshair)
                        {
                            DrawCrosshair(ctx, screenWidth, screenHeight);
                        }

                        DrawFPS(ctx, screenWidth, screenHeight);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogDebug($"ESP RENDER ERROR: {ex}");
            }
        }

        private void DrawLoot(Dx9RenderContext ctx, float screenWidth, float screenHeight)
        {
            var lootItems = Memory.Game?.Loot?.FilteredLoot;
            if (lootItems is null) return;

            foreach (var item in lootItems)
            {
                // Filter based on ESP settings
                bool isCorpse = item is LootCorpse;
                if (isCorpse && !App.Config.UI.EspCorpses)
                    continue;

                bool isContainer = item is LootContainer;
                if (isContainer && !App.Config.UI.EspContainers)
                    continue;

                bool isFood = item.IsFood;
                bool isMeds = item.IsMeds;
                bool isBackpack = item.IsBackpack;

                // Skip if it's one of these types and the setting is disabled
                if (isFood && !App.Config.UI.EspFood)
                    continue;
                if (isMeds && !App.Config.UI.EspMeds)
                    continue;
                if (isBackpack && !App.Config.UI.EspBackpacks)
                    continue;

                // Check distance to loot
                float distance = Vector3.Distance(_camPos, item.Position);
                if (App.Config.UI.EspLootMaxDistance > 0 && distance > App.Config.UI.EspLootMaxDistance)
                    continue;

                if (WorldToScreen2(item.Position, out var screen, screenWidth, screenHeight))
                {
                     // Calculate cone filter based on screen position
                     bool coneEnabled = App.Config.UI.EspLootConeEnabled && App.Config.UI.EspLootConeAngle > 0f;
                     bool inCone = true;

                     if (coneEnabled)
                     {
                         // Calculate angle from screen center
                         float centerX = screenWidth / 2f;
                         float centerY = screenHeight / 2f;
                         float dx = screen.X - centerX;
                         float dy = screen.Y - centerY;

                         // Calculate angular distance from center (in screen space)
                         // Using FOV to convert screen distance to angle
                         float fov = App.Config.UI.FOV;
                         float screenAngleX = MathF.Abs(dx / centerX) * (fov / 2f);
                         float screenAngleY = MathF.Abs(dy / centerY) * (fov / 2f);
                         float screenAngle = MathF.Sqrt(screenAngleX * screenAngleX + screenAngleY * screenAngleY);

                         inCone = screenAngle <= App.Config.UI.EspLootConeAngle;
                     }

                     // Determine colors based on item type
                     SKPaint circlePaint, textPaint;

                     if (item.Important)
                     {
                         // Filtered important items (custom filters) - Purple
                         circlePaint = SKPaints.PaintFilteredLoot;
                         textPaint = SKPaints.TextFilteredLoot;
                     }
                     else if (item.IsValuableLoot)
                     {
                         // Valuable items (price >= minValueValuable) - Turquoise
                         circlePaint = SKPaints.PaintImportantLoot;
                         textPaint = SKPaints.TextImportantLoot;
                     }
                     else if (isBackpack)
                     {
                         circlePaint = SKPaints.PaintBackpacks;
                         textPaint = SKPaints.TextBackpacks;
                     }
                     else if (isMeds)
                     {
                         circlePaint = SKPaints.PaintMeds;
                         textPaint = SKPaints.TextMeds;
                     }
                     else if (isFood)
                     {
                         circlePaint = SKPaints.PaintFood;
                         textPaint = SKPaints.TextFood;
                     }
                     else if (isCorpse)
                     {
                         circlePaint = SKPaints.PaintCorpse;
                         textPaint = SKPaints.TextCorpse;
                     }
                     else
                     {
                         circlePaint = _lootPaint;
                         textPaint = _lootTextPaint;
                     }

                     ctx.DrawCircle(ToRaw(screen), 2f, ToColor(circlePaint), true);

                     if (item.Important || inCone)
                     {
                         string text;
                         if (isCorpse && item is LootCorpse corpse)
                         {
                             var corpseName = corpse.Player?.Name;
                             text = string.IsNullOrWhiteSpace(corpseName) ? corpse.Name : corpseName;
                             if (App.Config.UI.EspLootPrice)
                             {
                                 var corpseValue = corpse.Loot?.Values?.Sum(x => x.Price) ?? 0;
                                 if (corpseValue > 0)
                                     text = $"{text} ({LoneEftDmaRadar.Misc.Utilities.FormatNumberKM(corpseValue)})";
                             }
                         }
                         else
                         {
                             var shortName = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
                             text = shortName;
                             if (App.Config.UI.EspLootPrice)
                             {
                                 text = item.Important
                                     ? shortName
                                     : $"{shortName} ({LoneEftDmaRadar.Misc.Utilities.FormatNumberKM(item.Price)})";
                             }
                         }
                         ctx.DrawText(text, screen.X + 4, screen.Y + 4, ToColor(textPaint), DxTextSize.Small);
                     }
                }
            }
        }

        /// <summary>
        /// Renders player on ESP
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawPlayerESP(Dx9RenderContext ctx, AbstractPlayer player, LocalPlayer localPlayer, float screenWidth, float screenHeight)
        {
            if (player is null || player == localPlayer || !player.IsAlive || !player.IsActive)
                return;

            // Check if this is AI or player
            bool isAI = player.Type is PlayerType.AIScav or PlayerType.AIRaider or PlayerType.AIBoss or PlayerType.PScav;

            // Optimization: Skip players/AI that are too far before W2S
            float distance = Vector3.Distance(localPlayer.Position, player.Position);
            float maxDistance = isAI ? App.Config.UI.EspAIMaxDistance : App.Config.UI.EspPlayerMaxDistance;

            // If maxDistance is 0, it means unlimited, otherwise check distance
            if (maxDistance > 0 && distance > maxDistance)
                return;

            // Fallback to old MaxDistance if the new settings aren't configured
            if (maxDistance == 0 && distance > App.Config.UI.MaxDistance)
                return;

            // Get Color
            var color = ToColor(GetPlayerColor(player));

            bool drawSkeleton = isAI ? App.Config.UI.EspAISkeletons : App.Config.UI.EspPlayerSkeletons;
            bool drawBox = isAI ? App.Config.UI.EspAIBoxes : App.Config.UI.EspPlayerBoxes;
            bool drawName = isAI ? App.Config.UI.EspAINames : App.Config.UI.EspPlayerNames;
            bool drawHealth = isAI ? App.Config.UI.EspAIHealth : App.Config.UI.EspPlayerHealth;
            bool drawDistance = isAI ? App.Config.UI.EspAIDistance : App.Config.UI.EspPlayerDistance;
            bool drawGroupId = isAI ? App.Config.UI.EspAIGroupIds : App.Config.UI.EspGroupIds;
            bool drawLabel = drawName || drawDistance || drawHealth || drawGroupId;

            // Draw Skeleton
            if (drawSkeleton)
            {
                DrawSkeleton(ctx, player, screenWidth, screenHeight, color, _skeletonPaint.StrokeWidth);
            }
            
            RectangleF bbox = default;
            bool hasBox = false;
            if (drawBox || drawLabel)
            {
                hasBox = TryGetBoundingBox(player, screenWidth, screenHeight, out bbox);
            }

            // Draw Box
            if (drawBox && hasBox)
            {
                DrawBoundingBox(ctx, bbox, color, _boxPaint.StrokeWidth);
            }

            // Draw head marker
            bool drawHeadCircle = isAI ? App.Config.UI.EspHeadCircleAI : App.Config.UI.EspHeadCirclePlayers;
            if (drawHeadCircle && TryProject(player.GetBonePos(Bones.HumanHead), screenWidth, screenHeight, out var headScreen))
            {
                float baseRadius = 4f;
                float fov = Math.Max(1f, _cameraManager.FOV);
                float scale = 50f / fov; // keeps size tighter at higher FOV values
                float radius = MathF.Max(2f, baseRadius * scale);
                ctx.DrawCircle(ToRaw(headScreen), radius, color, filled: false);
            }

            if (drawLabel)
            {
                DrawPlayerLabel(ctx, player, distance, color, hasBox ? bbox : (RectangleF?)null, screenWidth, screenHeight, drawName, drawDistance, drawHealth, drawGroupId);
            }
        }

        private void DrawSkeleton(Dx9RenderContext ctx, AbstractPlayer player, float w, float h, DxColor color, float thickness)
        {
            foreach (var (from, to) in _boneConnections)
            {
                var p1 = player.GetBonePos(from);
                var p2 = player.GetBonePos(to);

                if (TryProject(p1, w, h, out var s1) && TryProject(p2, w, h, out var s2))
                {
                    ctx.DrawLine(ToRaw(s1), ToRaw(s2), color, thickness);
                }
            }
        }

        private bool TryGetBoundingBox(AbstractPlayer player, float w, float h, out RectangleF rect)
        {
            rect = default;
            var projectedPoints = new List<SKPoint>();

            foreach (var boneKvp in player.PlayerBones)
            {
                if (TryProject(boneKvp.Value.Position, w, h, out var s))
                    projectedPoints.Add(s);
            }

            if (projectedPoints.Count < 2)
                return false;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in projectedPoints)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }

            float boxWidth = maxX - minX;
            float boxHeight = maxY - minY;

            if (boxWidth < 1f || boxHeight < 1f || boxWidth > w * 2f || boxHeight > h * 2f)
                return false;

            minX = Math.Clamp(minX, -50f, w + 50f);
            maxX = Math.Clamp(maxX, -50f, w + 50f);
            minY = Math.Clamp(minY, -50f, h + 50f);
            maxY = Math.Clamp(maxY, -50f, h + 50f);

            float padding = 2f;
            rect = new RectangleF(minX - padding, minY - padding, (maxX - minX) + padding * 2f, (maxY - minY) + padding * 2f);
            return true;
        }

        private void DrawBoundingBox(Dx9RenderContext ctx, RectangleF rect, DxColor color, float thickness)
        {
            ctx.DrawRect(rect, color, thickness);
        }

        /// <summary>
        /// Determines player color based on type
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static SKPaint GetPlayerColor(AbstractPlayer player)
        {
             if (player.IsFocused)
                return SKPaints.PaintAimviewWidgetFocused;
            if (player is LocalPlayer)
                return SKPaints.PaintAimviewWidgetLocalPlayer;

            if (player.Type == PlayerType.PMC)
            {
                if (App.Config.UI.EspGroupColors && player.GroupID >= 0 && !(player is LocalPlayer))
                {
                    return _espGroupPaints.GetOrAdd(player.GroupID, id =>
                    {
                        var color = _espGroupPalette[Math.Abs(id) % _espGroupPalette.Length];
                        return new SKPaint
                        {
                            Color = color,
                            StrokeWidth = SKPaints.PaintAimviewWidgetPMC.StrokeWidth,
                            Style = SKPaints.PaintAimviewWidgetPMC.Style,
                            IsAntialias = SKPaints.PaintAimviewWidgetPMC.IsAntialias
                        };
                    });
                }

                if (App.Config.UI.EspFactionColors)
                {
                    if (player.PlayerSide == Enums.EPlayerSide.Bear)
                        return SKPaints.PaintPMCBear;
                    if (player.PlayerSide == Enums.EPlayerSide.Usec)
                        return SKPaints.PaintPMCUsec;
                }

                return SKPaints.PaintPMC;
            }

            return player.Type switch
            {
                PlayerType.Teammate => SKPaints.PaintAimviewWidgetTeammate,
                PlayerType.AIScav => SKPaints.PaintAimviewWidgetScav,
                PlayerType.AIRaider => SKPaints.PaintAimviewWidgetRaider,
                PlayerType.AIBoss => SKPaints.PaintAimviewWidgetBoss,
                PlayerType.PScav => SKPaints.PaintAimviewWidgetPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                PlayerType.Streamer => SKPaints.PaintAimviewWidgetStreamer,
                _ => SKPaints.PaintAimviewWidgetPMC
            };
        }

        /// <summary>
        /// Draws player label (name/distance) relative to the bounding box or head fallback.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawPlayerLabel(Dx9RenderContext ctx, AbstractPlayer player, float distance, DxColor color, RectangleF? bbox, float screenWidth, float screenHeight, bool showName, bool showDistance, bool showHealth, bool showGroup)
        {
            if (!showName && !showDistance && !showHealth && !showGroup)
                return;

            var name = showName ? player.Name ?? "Unknown" : null;
            var distanceText = showDistance ? $"{distance:F0}m" : null;

            string healthText = null;
            if (showHealth && player is ObservedPlayer observed && observed.HealthStatus is not Enums.ETagStatus.Healthy)
                healthText = observed.HealthStatus.ToString();

            string factionText = null;
            if (App.Config.UI.EspPlayerFaction && player.IsPmc)
                factionText = player.PlayerSide.ToString();

            string groupText = null;
            if (showGroup && player.GroupID != -1 && player.IsPmc && !player.IsAI)
                groupText = $"G:{player.GroupID}";

            string text = name;
            if (!string.IsNullOrWhiteSpace(healthText))
                text = string.IsNullOrWhiteSpace(text) ? healthText : $"{text} ({healthText})";
            if (!string.IsNullOrWhiteSpace(distanceText))
                text = string.IsNullOrWhiteSpace(text) ? distanceText : $"{text} ({distanceText})";
            if (!string.IsNullOrWhiteSpace(groupText))
                text = string.IsNullOrWhiteSpace(text) ? groupText : $"{text} [{groupText}]";
            if (!string.IsNullOrWhiteSpace(factionText))
                text = string.IsNullOrWhiteSpace(text) ? factionText : $"{text} [{factionText}]";

            if (string.IsNullOrWhiteSpace(text))
                return;

            float drawX;
            float drawY;

            var bounds = ctx.MeasureText(text, DxTextSize.Medium);
            int textHeight = Math.Max(1, bounds.Bottom - bounds.Top);
            int textPadding = 6;

            var labelPos = player.IsAI ? App.Config.UI.EspLabelPositionAI : App.Config.UI.EspLabelPosition;

            if (bbox.HasValue)
            {
                var box = bbox.Value;
                drawX = box.Left + (box.Width / 2f);
                drawY = labelPos == EspLabelPosition.Top
                    ? box.Top - textHeight - textPadding
                    : box.Bottom + textPadding;
            }
            else if (TryProject(player.GetBonePos(Bones.HumanHead), screenWidth, screenHeight, out var headScreen))
            {
                drawX = headScreen.X;
                drawY = labelPos == EspLabelPosition.Top
                    ? headScreen.Y - textHeight - textPadding
                    : headScreen.Y + textPadding;
            }
            else
            {
                return;
            }

            ctx.DrawText(text, drawX, drawY, color, DxTextSize.Medium, centerX: true);
        }

        /// <summary>
        /// Draw 'ESP Hidden' notification.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void DrawNotShown(Dx9RenderContext ctx, float width, float height)
        {
            ctx.DrawText("ESP Hidden", width / 2f, height / 2f, new DxColor(255, 255, 255, 255), DxTextSize.Large, centerX: true, centerY: true);
        }

        private void DrawCrosshair(Dx9RenderContext ctx, float width, float height)
        {
            float centerX = width / 2f;
            float centerY = height / 2f;
            float length = MathF.Max(2f, App.Config.UI.EspCrosshairLength);

            var color = ToColor(_crosshairPaint);
            ctx.DrawLine(new RawVector2(centerX - length, centerY), new RawVector2(centerX + length, centerY), color, _crosshairPaint.StrokeWidth);
            ctx.DrawLine(new RawVector2(centerX, centerY - length), new RawVector2(centerX, centerY + length), color, _crosshairPaint.StrokeWidth);
        }

        private void DrawFPS(Dx9RenderContext ctx, float width, float height)
        {
            var fpsText = $"FPS: {_fps}";
            ctx.DrawText(fpsText, 10, 10, new DxColor(255, 255, 255, 255), DxTextSize.Small);
        }

        private static RawVector2 ToRaw(SKPoint point) => new(point.X, point.Y);

        private static DxColor ToColor(SKPaint paint) => ToColor(paint.Color);

        private static DxColor ToColor(SKColor color) => new(color.Blue, color.Green, color.Red, color.Alpha);

        #endregion

        private void ApplyDxFontConfig()
        {
            var ui = App.Config.UI;
            _dxOverlay?.SetFontConfig(
                ui.EspFontFamily,
                ui.EspFontSizeSmall,
                ui.EspFontSizeMedium,
                ui.EspFontSizeLarge);
        }

        #region DX Init Handling

        private void Overlay_DeviceInitFailed(Exception ex)
        {
            _dxInitFailed = true;
            DebugLogger.LogDebug($"ESP DX init failed: {ex}");

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RenderRoot.Children.Clear();
                RenderRoot.Children.Add(new TextBlock
                {
                    Text = "DX overlay init failed. See log for details.",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(12)
                });
            }), DispatcherPriority.Send);
        }

        #endregion

        #region WorldToScreen Conversion

        private TransposedViewMatrix _transposedViewMatrix = new();

        private void UpdateCameraPositionFromMatrix()
        {
            var viewMatrix = _cameraManager.ViewMatrix;
            _camPos = new Vector3(viewMatrix.M14, viewMatrix.M24, viewMatrix.M34);
            _transposedViewMatrix.Update(ref viewMatrix);
        }

        private bool WorldToScreen2(in Vector3 world, out SKPoint scr, float screenWidth, float screenHeight)
        {
            scr = default;

            float w = Vector3.Dot(_transposedViewMatrix.Translation, world) + _transposedViewMatrix.M44;
            
            if (w < 0.098f)
                return false;
            
            float x = Vector3.Dot(_transposedViewMatrix.Right, world) + _transposedViewMatrix.M14;
            float y = Vector3.Dot(_transposedViewMatrix.Up, world) + _transposedViewMatrix.M24;
            
            var centerX = screenWidth / 2f;
            var centerY = screenHeight / 2f;
            
            scr.X = centerX * (1f + x / w);
            scr.Y = centerY * (1f - y / w);
            
            return true;
        }

        private class TransposedViewMatrix
        {
            public float M44;
            public float M14;
            public float M24;
            public Vector3 Translation;
            public Vector3 Right;
            public Vector3 Up;
            public Vector3 Forward;

            public void Update(ref Matrix4x4 matrix)
            {
                M44 = matrix.M44;
                M14 = matrix.M41;
                M24 = matrix.M42;

                Translation.X = matrix.M14;
                Translation.Y = matrix.M24;
                Translation.Z = matrix.M34;

                Right.X = matrix.M11;
                Right.Y = matrix.M21;
                Right.Z = matrix.M31;

                Up.X = matrix.M12;
                Up.Y = matrix.M22;
                Up.Z = matrix.M32;

                // In Unity's View Matrix, forward is the negative Z-axis
                // X is negated to match the horizontal orientation in EFT
                Forward.X = matrix.M13;
                Forward.Y = -matrix.M23;
                Forward.Z = -matrix.M33;
            }
        }

        private bool TryProject(in Vector3 world, float w, float h, out SKPoint screen)
        {
            screen = default;
            if (world == Vector3.Zero)
                return false;
            if (!WorldToScreen2(world, out screen, w, h))
                return false;
            if (float.IsNaN(screen.X) || float.IsInfinity(screen.X) ||
                float.IsNaN(screen.Y) || float.IsInfinity(screen.Y))
                return false;

            const float margin = 200f; 
            if (screen.X < -margin || screen.X > w + margin ||
                screen.Y < -margin || screen.Y > h + margin)
                return false;

            return true;
        }

        #endregion

        #region Window Management

        private void GlControl_MouseDown(object sender, WinForms.MouseEventArgs e)
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                try { this.DragMove(); } catch { /* ignore dragging errors */ }
            }
        }

        private void GlControl_DoubleClick(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        private void GlControl_KeyDown(object sender, WinForms.KeyEventArgs e)
        {
            if (e.KeyCode == WinForms.Keys.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _isClosing = true;
            try
            {
                _highFrequencyTimer?.Dispose();
                _dxOverlay?.Dispose();
                _skeletonPaint.Dispose();
                _boxPaint.Dispose();
                _lootPaint.Dispose();
                _lootTextPaint.Dispose();
                _crosshairPaint.Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP: OnClosed cleanup error: {ex}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // Method to force refresh
        public void RefreshESP()
        {
            if (_isClosing)
                return;

            try
            {
                _dxOverlay?.Render();
            }
            catch (Exception ex)
            {
                DebugLogger.LogDebug($"ESP Refresh error: {ex}");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _renderPending, 0);
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleFullscreen();
        }

        // Handler for keys (ESC to exit fullscreen)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && this.WindowState == WindowState.Maximized)
            {
                ToggleFullscreen();
            }
        }

        // Simple fullscreen toggle
        public void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.Topmost = false;
                this.ResizeMode = ResizeMode.CanResize;
                this.Width = 400;
                this.Height = 300;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _isFullscreen = false;
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.Topmost = true;
                this.WindowState = WindowState.Normal;

                // Get target screen
                var targetScreenIndex = App.Config.UI.EspTargetScreen;
                var (width, height) = GetConfiguredResolution();

                // Position window based on screen selection
                if (targetScreenIndex == 0)
                {
                    // Primary screen - position at 0,0
                    this.Left = 0;
                    this.Top = 0;
                    if (width == SystemParameters.PrimaryScreenWidth && height == SystemParameters.PrimaryScreenHeight)
                    {
                        width = SystemParameters.PrimaryScreenWidth;
                        height = SystemParameters.PrimaryScreenHeight;
                    }
                }
                else
                {
                    // Secondary screen - position to the right of primary
                    var primaryWidth = SystemParameters.PrimaryScreenWidth;
                    var virtualLeft = SystemParameters.VirtualScreenLeft;
                    var virtualTop = SystemParameters.VirtualScreenTop;

                    // If secondary is to the left (negative coords)
                    if (virtualLeft < 0)
                    {
                        this.Left = virtualLeft;
                        this.Top = virtualTop;
                    }
                    else
                    {
                        // Secondary is to the right
                        this.Left = primaryWidth;
                        this.Top = 0;
                    }

                    if (width == SystemParameters.PrimaryScreenWidth && height == SystemParameters.PrimaryScreenHeight)
                    {
                        // Use virtual screen dimensions for secondary
                        width = SystemParameters.VirtualScreenWidth - SystemParameters.PrimaryScreenWidth;
                        height = SystemParameters.VirtualScreenHeight;
                    }
                }

                this.Width = width;
                this.Height = height;
                _isFullscreen = true;
            }

            this.RefreshESP();
        }

        public void ApplyResolutionOverride()
        {
            if (!_isFullscreen)
                return;

            var (width, height) = GetConfiguredResolution();
            this.Left = 0;
            this.Top = 0;
            this.Width = width;
            this.Height = height;
            this.RefreshESP();
        }

        private (double width, double height) GetConfiguredResolution()
        {
            double width = App.Config.UI.EspScreenWidth > 0
                ? App.Config.UI.EspScreenWidth
                : SystemParameters.PrimaryScreenWidth;
            double height = App.Config.UI.EspScreenHeight > 0
                ? App.Config.UI.EspScreenHeight
                : SystemParameters.PrimaryScreenHeight;
            return (width, height);
        }

        private void ApplyResolutionOverrideIfNeeded()
        {
            if (!_isFullscreen)
                return;

            if (App.Config.UI.EspScreenWidth <= 0 && App.Config.UI.EspScreenHeight <= 0)
                return;

            var target = GetConfiguredResolution();
            if (Math.Abs(Width - target.width) > 0.5 || Math.Abs(Height - target.height) > 0.5)
            {
                Width = target.width;
                Height = target.height;
                Left = 0;
                Top = 0;
            }
        }

        public void ApplyFontConfig()
        {
            ApplyDxFontConfig();
            RefreshESP();
        }

        #endregion
    }
}
