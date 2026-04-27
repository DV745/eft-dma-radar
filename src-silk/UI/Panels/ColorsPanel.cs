using eft_dma_radar.Silk.UI.Widgets;
using ImGuiNET;

namespace eft_dma_radar.Silk.UI.Panels
{
    /// <summary>
    /// Settings sub-panel that exposes color pickers for all configurable radar colors.
    /// </summary>
    internal static class ColorsPanel
    {
        private static SilkConfig Config => SilkProgram.Config;

        private const ImGuiColorEditFlags PickerFlags =
            ImGuiColorEditFlags.NoInputs |
            ImGuiColorEditFlags.AlphaBar |
            ImGuiColorEditFlags.AlphaPreview;

        private const ImGuiTableFlags TableFlags =
            ImGuiTableFlags.SizingFixedFit |
            ImGuiTableFlags.BordersInnerV;

        // ŌöĆŌöĆ Helpers ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ

        /// <summary>Convert a packed ARGB uint (0xAARRGGBB) to a normalized RGBA Vector4 for ImGui.</summary>
        private static Vector4 ToVec4(uint argb) => new(
            (argb >> 16 & 0xFF) / 255f,
            (argb >>  8 & 0xFF) / 255f,
            (argb        & 0xFF) / 255f,
            (argb >> 24  & 0xFF) / 255f);

        /// <summary>Convert a normalized RGBA Vector4 back to a packed ARGB uint.</summary>
        private static uint ToArgb(Vector4 v)
        {
            uint a = (uint)MathF.Round(float.Clamp(v.W, 0f, 1f) * 255f);
            uint r = (uint)MathF.Round(float.Clamp(v.X, 0f, 1f) * 255f);
            uint g = (uint)MathF.Round(float.Clamp(v.Y, 0f, 1f) * 255f);
            uint b = (uint)MathF.Round(float.Clamp(v.Z, 0f, 1f) * 255f);
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        /// <summary>
        /// Draw a compact color edit widget. Returns true and writes <paramref name="next"/>
        /// when the user changes the color, otherwise <paramref name="next"/> equals <paramref name="current"/>.
        /// </summary>
        private static bool CE(string id, uint current, out uint next)
        {
            var v = ToVec4(current);
            bool changed = ImGui.ColorEdit4(id, ref v, PickerFlags);
            next = ToArgb(v);
            return changed;
        }

        // ŌöĆŌöĆ Draw ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ

        /// <summary>Draw the full colors configuration UI. Called inside a tab item.</summary>
        public static void Draw()
        {
            var cfg = Config;
            bool anyChanged = false;

            // ŌöĆŌöĆ Player Colors ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ
            ImGui.Spacing();
            ImGui.SeparatorText("Player Colors");
            ImGui.Spacing();

            if (ImGui.BeginTable("##player_colors", 4, TableFlags))
            {
                ImGui.TableSetupColumn("##pl1", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##pc1", ImGuiTableColumnFlags.WidthFixed, 52f);
                ImGui.TableSetupColumn("##pl2", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##pc2", ImGuiTableColumnFlags.WidthFixed, 52f);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Local Player");
                ImGui.TableSetColumnIndex(1);
                if (CE("##clp", cfg.ColorLocalPlayer, out uint nLp)) { cfg.ColorLocalPlayer = nLp; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Teammate");
                ImGui.TableSetColumnIndex(3);
                if (CE("##ctm", cfg.ColorTeammate, out uint nTm)) { cfg.ColorTeammate = nTm; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("USEC");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cusec", cfg.ColorUSEC, out uint nUsec)) { cfg.ColorUSEC = nUsec; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("BEAR");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cbear", cfg.ColorBEAR, out uint nBear)) { cfg.ColorBEAR = nBear; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Scav");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cscav", cfg.ColorScav, out uint nScav)) { cfg.ColorScav = nScav; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Raider");
                ImGui.TableSetColumnIndex(3);
                if (CE("##craider", cfg.ColorRaider, out uint nRaider)) { cfg.ColorRaider = nRaider; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Boss");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cboss", cfg.ColorBoss, out uint nBoss)) { cfg.ColorBoss = nBoss; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Player Scav");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cpscav", cfg.ColorPScav, out uint nPScav)) { cfg.ColorPScav = nPScav; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Special");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cspecial", cfg.ColorSpecial, out uint nSpecial)) { cfg.ColorSpecial = nSpecial; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Streamer");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cstreamer", cfg.ColorStreamer, out uint nStreamer)) { cfg.ColorStreamer = nStreamer; anyChanged = true; }

                ImGui.EndTable();
            }

            // ŌöĆŌöĆ Loot Colors ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ
            ImGui.Spacing();
            ImGui.SeparatorText("Loot Colors");
            ImGui.Spacing();

            if (ImGui.BeginTable("##loot_colors", 4, TableFlags))
            {
                ImGui.TableSetupColumn("##ll1", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##lc1", ImGuiTableColumnFlags.WidthFixed, 52f);
                ImGui.TableSetupColumn("##ll2", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##lc2", ImGuiTableColumnFlags.WidthFixed, 52f);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Regular Loot");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cloot_norm", cfg.ColorLootNormal, out uint nLootNorm)) { cfg.ColorLootNormal = nLootNorm; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Important Loot");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cloot_imp", cfg.ColorLootImportant, out uint nLootImp)) { cfg.ColorLootImportant = nLootImp; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Wishlist");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cloot_wl", cfg.ColorLootWishlist, out uint nLootWL)) { cfg.ColorLootWishlist = nLootWL; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Quest Items");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cloot_quest", cfg.ColorLootQuestItems, out uint nLootQuest)) { cfg.ColorLootQuestItems = nLootQuest; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Meds");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cloot_meds", cfg.ColorLootMeds, out uint nLootMeds)) { cfg.ColorLootMeds = nLootMeds; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Food");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cloot_food", cfg.ColorLootFood, out uint nLootFood)) { cfg.ColorLootFood = nLootFood; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Backpacks");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cloot_packs", cfg.ColorLootBackpacks, out uint nLootPacks)) { cfg.ColorLootBackpacks = nLootPacks; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Keys");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cloot_keys", cfg.ColorLootKeys, out uint nLootKeys)) { cfg.ColorLootKeys = nLootKeys; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Corpse");
                ImGui.TableSetColumnIndex(1);
                if (CE("##ccorpse", cfg.ColorCorpse, out uint nCorpse)) { cfg.ColorCorpse = nCorpse; anyChanged = true; }

                ImGui.EndTable();
            }

            // ŌöĆŌöĆ Map Colors ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ
            ImGui.Spacing();
            ImGui.SeparatorText("Map Colors");
            ImGui.Spacing();

            if (ImGui.BeginTable("##map_colors", 4, TableFlags))
            {
                ImGui.TableSetupColumn("##ml1", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##mc1", ImGuiTableColumnFlags.WidthFixed, 52f);
                ImGui.TableSetupColumn("##ml2", ImGuiTableColumnFlags.WidthFixed, 140f);
                ImGui.TableSetupColumn("##mc2", ImGuiTableColumnFlags.WidthFixed, 52f);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Exfil Open");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cexfil_open", cfg.ColorExfilOpen, out uint nExfilOpen)) { cfg.ColorExfilOpen = nExfilOpen; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Exfil Pending");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cexfil_pend", cfg.ColorExfilPending, out uint nExfilPend)) { cfg.ColorExfilPending = nExfilPend; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Exfil Closed");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cexfil_close", cfg.ColorExfilClosed, out uint nExfilClose)) { cfg.ColorExfilClosed = nExfilClose; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Exfil Inactive");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cexfil_inact", cfg.ColorExfilInactive, out uint nExfilInact)) { cfg.ColorExfilInactive = nExfilInact; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Door Locked");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cdoor_lock", cfg.ColorDoorLocked, out uint nDoorLock)) { cfg.ColorDoorLocked = nDoorLock; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Door Open");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cdoor_open", cfg.ColorDoorOpen, out uint nDoorOpen)) { cfg.ColorDoorOpen = nDoorOpen; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Door Shut");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cdoor_shut", cfg.ColorDoorShut, out uint nDoorShut)) { cfg.ColorDoorShut = nDoorShut; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Group Lines");
                ImGui.TableSetColumnIndex(3);
                if (CE("##cgroup", cfg.ColorGroupLines, out uint nGroup)) { cfg.ColorGroupLines = nGroup; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Death Marker");
                ImGui.TableSetColumnIndex(1);
                if (CE("##cdeath", cfg.ColorDeathMarker, out uint nDeath)) { cfg.ColorDeathMarker = nDeath; anyChanged = true; }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.AlignTextToFramePadding(); ImGui.Text("Transit");
                ImGui.TableSetColumnIndex(1);
                if (CE("##ctransit", cfg.ColorTransit, out uint nTransit)) { cfg.ColorTransit = nTransit; anyChanged = true; }
                ImGui.TableSetColumnIndex(2); ImGui.AlignTextToFramePadding(); ImGui.Text("Transit Inactive");
                ImGui.TableSetColumnIndex(3);
                if (CE("##ctransit_inact", cfg.ColorTransitInactive, out uint nTransitInact)) { cfg.ColorTransitInactive = nTransitInact; anyChanged = true; }

                ImGui.EndTable();
            }

            if (anyChanged)
            {
                SKPaints.ApplyColors(cfg);
                AimviewWidget.InvalidateColors();
                cfg.MarkDirty();
            }

            // ŌöĆŌöĆ Footer ŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆŌöĆ
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Reset to Defaults"))
            {
                cfg.ResetColors();
                SKPaints.ApplyColors(cfg);
                AimviewWidget.InvalidateColors();
                cfg.MarkDirty();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Reset all colors to their default values");
        }
    }
}

