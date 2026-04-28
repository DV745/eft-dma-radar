using eft_dma_radar.Arena.GameWorld;
using ImGuiNET;
using SDK;

namespace eft_dma_radar.Arena.UI
{
    internal static partial class RadarWindow
    {
        // Reusable sorted list — avoids per-frame allocation.
        private static readonly List<Player> _plSorted = new(32);
        private static Vector3 _plSortOrigin;

        // Column header colors
        private static readonly Vector4 _plColorDim     = new(0.40f, 0.40f, 0.40f, 1f);
        private static readonly Vector4 _plColorLocal    = new(0.20f, 1.00f, 1.00f, 1f);
        private static readonly Vector4 _plColorTeammate = new(0.39f, 0.63f, 1.00f, 1f);
        private static readonly Vector4 _plColorEnemy    = new(0.90f, 0.24f, 0.24f, 1f);
        private static readonly Vector4 _plColorScav     = new(0.94f, 0.90f, 0.24f, 1f);
        private static readonly Vector4 _plColorRaider   = new(0.86f, 0.39f, 0.71f, 1f);
        private static readonly Vector4 _plColorBoss     = new(0.71f, 0.20f, 0.86f, 1f);
        private static readonly Vector4 _plColorGuard    = new(0.86f, 0.39f, 0.71f, 1f);
        private static readonly Vector4 _plColorPScav    = new(0.90f, 0.24f, 0.24f, 1f);
        private static readonly Vector4 _plColorDefault  = new(0.80f, 0.80f, 0.80f, 1f);

        // Team armband colors
        private static readonly Vector4 _plTeamRed     = new(0.95f, 0.25f, 0.25f, 1f);
        private static readonly Vector4 _plTeamFuchsia = new(0.95f, 0.30f, 0.85f, 1f);
        private static readonly Vector4 _plTeamYellow  = new(0.95f, 0.90f, 0.25f, 1f);
        private static readonly Vector4 _plTeamGreen   = new(0.30f, 0.85f, 0.30f, 1f);
        private static readonly Vector4 _plTeamAzure   = new(0.30f, 0.75f, 0.95f, 1f);
        private static readonly Vector4 _plTeamWhite   = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Vector4 _plTeamBlue    = new(0.30f, 0.50f, 0.95f, 1f);

        /// <summary>Draws the Player List ImGui window when enabled.</summary>
        private static void DrawPlayerListWidget()
        {
            if (!Config.PlayerListEnabled) return;

            ImGui.SetNextWindowSizeConstraints(new Vector2(340, 180), new Vector2(900, 700));
            ImGui.SetNextWindowSize(new Vector2(520, 320), ImGuiCond.FirstUseEver);

            bool open = Config.PlayerListEnabled;
            var flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse;

            if (!ImGui.Begin("Players", ref open, flags))
            {
                if (open != Config.PlayerListEnabled) Config.PlayerListEnabled = open;
                ImGui.End();
                return;
            }

            try
            {
                if (open != Config.PlayerListEnabled) Config.PlayerListEnabled = open;

                var gw = Memory.CurrentGameWorld;
                var local = gw?.LocalPlayer;

                if (gw is null || local is null)
                {
                    ImGui.TextColored(_plColorDim, "Waiting for match...");
                    return;
                }

                var localPos = local.HasValidPosition ? local.Position : Vector3.Zero;

                // ── Summary counts ───────────────────────────────────────────
                int teammates = 0, enemies = 0, scavs = 0, ai = 0, bosses = 0;
                _plSorted.Clear();

                foreach (var p in gw.Players)
                {
                    if (p.IsLocalPlayer || !p.IsActive) continue;

                    if (p.IsAlive && p.HasValidPosition)
                    {
                        switch (p.Type)
                        {
                            case PlayerType.Teammate:                           teammates++; break;
                            case PlayerType.USEC or PlayerType.BEAR:           enemies++;   break;
                            case PlayerType.PScav:                              scavs++;     break;
                            case PlayerType.AIScav or PlayerType.AIRaider:     ai++;        break;
                            case PlayerType.AIBoss or PlayerType.AIGuard:      bosses++;    break;
                        }
                    }

                    _plSorted.Add(p);
                }

                // Sort: alive first, then by distance ascending
                _plSortOrigin = localPos;
                _plSorted.Sort(static (a, b) =>
                {
                    // Alive before dead
                    int aliveCmp = b.IsAlive.CompareTo(a.IsAlive);
                    if (aliveCmp != 0) return aliveCmp;

                    // Then closer first
                    float da = Vector3.DistanceSquared(_plSortOrigin, a.Position);
                    float db = Vector3.DistanceSquared(_plSortOrigin, b.Position);
                    return da.CompareTo(db);
                });

                // Summary line
                ImGui.TextColored(new Vector4(0.65f, 0.65f, 0.65f, 1f),
                    $"Teammates: {teammates}   Enemies: {enemies}   PScav: {scavs}   AI: {ai}   Boss: {bosses}");
                ImGui.Separator();

                // ── Table ────────────────────────────────────────────────────
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(4, 2));

                var tableFlags = ImGuiTableFlags.Borders      |
                                 ImGuiTableFlags.RowBg        |
                                 ImGuiTableFlags.Resizable    |
                                 ImGuiTableFlags.ScrollY      |
                                 ImGuiTableFlags.SizingFixedFit |
                                 ImGuiTableFlags.NoPadOuterX;

                if (ImGui.BeginTable("ArenaPlayersTable", 8, tableFlags))
                {
                    ImGui.TableSetupColumn("Name",   ImGuiTableColumnFlags.WidthFixed,   160f);
                    ImGui.TableSetupColumn("Type",   ImGuiTableColumnFlags.WidthFixed,    64f);
                    ImGui.TableSetupColumn("Team",   ImGuiTableColumnFlags.WidthFixed,    50f);
                    ImGui.TableSetupColumn("Dist",   ImGuiTableColumnFlags.WidthFixed,    44f);
                    ImGui.TableSetupColumn("Δ Alt",  ImGuiTableColumnFlags.WidthFixed,    40f);
                    ImGui.TableSetupColumn("Yaw",    ImGuiTableColumnFlags.WidthFixed,    44f);
                    ImGui.TableSetupColumn("Health", ImGuiTableColumnFlags.WidthFixed,    80f);
                    ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed,    50f);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    foreach (var p in _plSorted)
                    {
                        ImGui.TableNextRow();
                        var color = GetPlayerListColor(p);

                        // ── Name ──
                        ImGui.TableNextColumn();
                        string displayName = string.IsNullOrEmpty(p.Name) ? "(unknown)" : p.Name;
                        ImGui.TextColored(color, displayName);

                        // Hover tooltip with extra details
                        if (ImGui.IsItemHovered())
                            DrawPlayerListTooltip(p, localPos);

                        // ── Type ──
                        ImGui.TableNextColumn();
                        ImGui.TextColored(color, GetPlayerTypeLabel(p));

                        // ── Team ──
                        ImGui.TableNextColumn();
                        if (p.IsLocalPlayer)
                        {
                            ImGui.TextColored(_plColorLocal, "You");
                        }
                        else if (p.Type == PlayerType.Teammate)
                        {
                            ImGui.TextColored(_plColorTeammate, "Blue");
                        }
                        else
                        {
                            ImGui.TextColored(_plColorEnemy, "Red");
                        }

                        // ── Distance ──
                        ImGui.TableNextColumn();
                        if (p.HasValidPosition)
                            ImGui.TextColored(color, $"{(int)Vector3.Distance(localPos, p.Position)}m");
                        else
                            ImGui.TextColored(_plColorDim, "--");

                        // ── Altitude delta ──
                        ImGui.TableNextColumn();
                        if (p.HasValidPosition)
                        {
                            int alt = (int)MathF.Round(p.Position.Y - localPos.Y);
                            ImGui.TextColored(color, $"{alt:+0;-0;0}");
                        }
                        else
                        {
                            ImGui.TextColored(_plColorDim, "--");
                        }

                        // ── Yaw ──
                        ImGui.TableNextColumn();
                        if (p.HasValidPosition)
                            ImGui.TextColored(color, $"{(int)p.RotationYaw}°");
                        else
                            ImGui.TextColored(_plColorDim, "--");

                        // ── Health ──
                        ImGui.TableNextColumn();
                        if (p.IsLocalPlayer)
                        {
                            ImGui.TextColored(_plColorDim, "--");
                        }
                        else
                        {
                            var (healthLabel, healthColor) = p.HealthStatus switch
                            {
                                PlayerHealthStatus.Healthy      => ("Healthy",       new Vector4(0.30f, 0.85f, 0.30f, 1f)),
                                PlayerHealthStatus.Injured      => ("Injured",       new Vector4(0.90f, 0.78f, 0.20f, 1f)),
                                PlayerHealthStatus.BadlyInjured => ("Badly Injured", new Vector4(0.90f, 0.47f, 0.12f, 1f)),
                                _                               => ("Dying",         new Vector4(0.86f, 0.16f, 0.16f, 1f)),
                            };
                            ImGui.TextColored(healthColor, healthLabel);
                        }

                        // ── Status ──
                        ImGui.TableNextColumn();
                        if (!p.IsAlive)
                            ImGui.TextColored(_plColorDim, "Dead");
                        else if (!p.IsActive)
                            ImGui.TextColored(_plColorDim, "Inactive");
                        else
                            ImGui.TextColored(new Vector4(0.30f, 0.85f, 0.30f, 1f), "Alive");
                    }

                    ImGui.EndTable();
                }

                ImGui.PopStyleVar();
            }
            finally
            {
                ImGui.End();
            }
        }

        private static void DrawPlayerListTooltip(Player p, Vector3 localPos)
        {
            ImGui.BeginTooltip();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 2));

            var color = GetPlayerListColor(p);

            // Identity
            ImGui.TextColored(color, string.IsNullOrEmpty(p.Name) ? "(unknown)" : p.Name);
            ImGui.SameLine();
            ImGui.TextColored(_plColorDim, $"  [{GetPlayerTypeLabel(p)}]");

            if (!string.IsNullOrEmpty(p.ProfileId))
            {
                ImGui.TextColored(_plColorDim, "Profile: ");
                ImGui.SameLine();
                ImGui.TextUnformatted(p.ProfileId);
            }

            if (p.TeamID >= 0)
            {
                ImGui.TextColored(_plColorDim, "Team: ");
                ImGui.SameLine();
                bool isFriendly = p.Type == PlayerType.Teammate;
                ImGui.TextColored(
                    isFriendly ? _plColorTeammate : _plColorEnemy,
                    isFriendly ? "Blue" : "Red");
            }

            if (p.HasValidPosition)
            {
                ImGui.Spacing();
                int dist = (int)Vector3.Distance(localPos, p.Position);
                int alt  = (int)MathF.Round(p.Position.Y - localPos.Y);
                ImGui.TextColored(_plColorDim, $"Dist: {dist}m   Alt: {alt:+0;-0;0}m");
                ImGui.TextColored(_plColorDim, $"Yaw: {(int)p.RotationYaw}°   Pitch: {p.RotationPitch:F1}°");
                ImGui.TextColored(_plColorDim,
                    $"Pos: <{p.Position.X:F1}, {p.Position.Y:F1}, {p.Position.Z:F1}>");
            }

            ImGui.PopStyleVar();
            ImGui.EndTooltip();
        }

        private static Vector4 GetPlayerListColor(Player p)
        {
            if (p.IsLocalPlayer)               return _plColorLocal;
            if (p.Type == PlayerType.Teammate) return _plColorTeammate;
            return p.Type switch
            {
                PlayerType.AIScav   => _plColorScav,
                PlayerType.AIRaider => _plColorRaider,
                PlayerType.AIBoss   => _plColorBoss,
                PlayerType.AIGuard  => _plColorGuard,
                _                   => _plColorEnemy, // red for human enemies
            };
        }

        private static string GetPlayerTypeLabel(Player p)
        {
            if (p.IsLocalPlayer) return "You";
            return p.Type switch
            {
                PlayerType.Teammate => "Teammate",
                PlayerType.USEC     => "USEC",
                PlayerType.BEAR     => "BEAR",
                PlayerType.PScav    => "PScav",
                PlayerType.AIScav   => "AI Scav",
                PlayerType.AIRaider => "Raider",
                PlayerType.AIBoss   => "Boss",
                PlayerType.AIGuard  => "Guard",
                _                   => "Unknown",
            };
        }

        private static Vector4 GetTeamColor(ArmbandColorType team) => team switch
        {
            ArmbandColorType.red     => _plTeamRed,
            ArmbandColorType.fuchsia => _plTeamFuchsia,
            ArmbandColorType.yellow  => _plTeamYellow,
            ArmbandColorType.green   => _plTeamGreen,
            ArmbandColorType.azure   => _plTeamAzure,
            ArmbandColorType.white   => _plTeamWhite,
            ArmbandColorType.blue    => _plTeamBlue,
            _                        => _plColorDefault,
        };
    }
}
