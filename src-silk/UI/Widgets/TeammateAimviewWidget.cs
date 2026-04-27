using ImGuiNET;

namespace eft_dma_radar.Silk.UI.Widgets
{
    /// <summary>
    /// Aimview widget that renders the world from a teammate's perspective.
    /// Type the teammate's in-game name to see where they are looking.
    /// </summary>
    internal static class TeammateAimviewWidget
    {
        private static bool _isOpen;
        private static string _selectedName = string.Empty;

        /// <summary>Whether the teammate aimview widget is open.</summary>
        public static bool IsOpen
        {
            get => _isOpen;
            set => _isOpen = value;
        }

        /// <summary>The teammate name currently tracked. Persisted across sessions.</summary>
        public static string SelectedName
        {
            get => _selectedName;
            set => _selectedName = value ?? string.Empty;
        }

        /// <summary>Draw the teammate aimview ImGui window.</summary>
        public static void Draw()
        {
            bool isOpen = _isOpen;
            ImGui.SetNextWindowSize(new Vector2(360, 270), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(200, 160), new Vector2(800, 640));

            var flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            if (!ImGui.Begin("Teammate Aimview", ref isOpen, flags))
            {
                _isOpen = isOpen;
                ImGui.End();
                return;
            }
            _isOpen = isOpen;

            // Name input ŌĆö fills the full content width
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("##teammate_name", "Enter teammate name...", ref _selectedName, 64);

            ImGui.Spacing();

            // Resolve the named teammate from the current player list
            var allPlayers = Memory.Players;
            Player? teammate = null;
            if (!string.IsNullOrWhiteSpace(_selectedName) && allPlayers is not null)
            {
                foreach (var player in allPlayers)
                {
                    if (player.IsLocalPlayer || !player.IsActive || !player.IsAlive)
                        continue;
                    if (player.Name.Equals(_selectedName, StringComparison.OrdinalIgnoreCase))
                    {
                        teammate = player;
                        break;
                    }
                }
            }

            var contentMin = ImGui.GetCursorScreenPos();
            var contentSize = ImGui.GetContentRegionAvail();

            if (contentSize.X < 10 || contentSize.Y < 10)
            {
                ImGui.End();
                return;
            }

            if (teammate is null || !teammate.HasValidPosition)
            {
                string msg = string.IsNullOrWhiteSpace(_selectedName)
                    ? "Enter a teammate name above"
                    : $"'{_selectedName}' not found in raid";
                var textSize = ImGui.CalcTextSize(msg);
                var offset = (contentSize - textSize) * 0.5f;
                ImGui.GetWindowDrawList().AddText(
                    contentMin + offset,
                    ImGui.GetColorU32(new Vector4(0.55f, 0.55f, 0.58f, 1f)),
                    msg);
                ImGui.End();
                return;
            }

            ImGui.InvisibleButton("##teammate_aimview_canvas", contentSize);
            var drawList = ImGui.GetWindowDrawList();
            var contentMax = contentMin + contentSize;

            AimviewWidget.DrawContent(drawList, teammate, contentMin, contentSize, contentMax);

            ImGui.End();
        }
    }
}

