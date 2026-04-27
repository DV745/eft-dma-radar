namespace eft_dma_radar.Silk.Tarkov.GameWorld.Player
{
    /// <summary>
    /// Player rendering — marker geometry, draw methods, and paint selection.
    /// </summary>
    public partial class Player
    {
        #region Marker Geometry

        // Core dot — small filled circle
        private const float DotRadius = 5f;

        // Chevron stroke paints
        private static readonly SKPaint _chevronOutline = new()
        {
            Color = new SKColor(0, 0, 0, 160),
            StrokeWidth = 3.2f,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };

        // Height indicator threshold — draw ▲/▼ when height difference exceeds this (meters)
        private const float HEIGHT_INDICATOR_THRESHOLD = 1.85f;
        private const float HEIGHT_INDICATOR_ARROW_SIZE = 4.5f;

        // Small font for the compact H/D info line
        private static readonly SKFont _infoFont = new(CustomFonts.Regular, 9.5f) { Subpixel = true };

        private static readonly SKPaint _infoPaint = new()
        {
            Color = new SKColor(200, 200, 200, 190),
            IsStroke = false,
            IsAntialias = true,
        };

        private static readonly SKPaint _infoShadow = new()
        {
            Color = new SKColor(0, 0, 0, 180),
            IsStroke = false,
            IsAntialias = true,
        };

        // Weapon/ammo line paint — slightly dimmer than the name
        private static readonly SKPaint _weaponPaint = new()
        {
            Color = new SKColor(200, 200, 200, 190),
            IsStroke = false,
            IsAntialias = true,
        };

        // Health status indicator paints
        private static readonly SKPaint _healthInjured    = new() { Color = new SKColor(230, 200,  50, 220), IsAntialias = true };
        private static readonly SKPaint _healthBadly      = new() { Color = new SKColor(230, 120,  30, 220), IsAntialias = true };
        private static readonly SKPaint _healthDying      = new() { Color = new SKColor(220,  40,  40, 220), IsAntialias = true };

        // Local player weapon/energy label paints
        private static readonly SKPaint _localInfoPaint = new()
        {
            Color = new SKColor(180, 180, 180, 200),
            IsStroke = false,
            IsAntialias = true,
        };
        private static readonly SKFont _localInfoFont = new(CustomFonts.Regular, 9f) { Subpixel = true };

        // Aimline paint — semi-transparent, thin line extending from the dot
        private static readonly SKPaint _aimlineOutline = new()
        {
            Color = new SKColor(0, 0, 0, 120),
            StrokeWidth = 2.6f,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };

        // Radians conversion constant
        private const float DegToRad = MathF.PI / 180f;

        // High Alert aimline length (extends to edge of radar when enemy is facing you)
        private const float HighAlertLength = 2000f;

        #endregion

        #region Draw

        // Cached info line — avoids per-frame string allocation
        private int _cachedInfoH = int.MinValue;
        private int _cachedInfoD = int.MinValue;
        private string? _cachedInfo;

        /// <summary>
        /// Draws this player on the radar canvas.
        /// </summary>
        internal virtual void Draw(SKCanvas canvas, SKPoint pos, Player? localPlayer = null)
        {
            if (!IsAlive)
            {
                DrawDeathMarker(canvas, pos);
                return;
            }

            var (fillPaint, textPaint, chevronPaint, aimlinePaint) = GetPaints();

            // Compute rotation sin/cos once — shared by marker + aimline
            float rad = MapRotation * DegToRad;
            (float sin, float cos) = MathF.SinCos(rad);

            DrawMarker(canvas, pos, fillPaint, chevronPaint, sin, cos);

            // Aimline — draw after marker so it extends outward (local player included)
            if (SilkProgram.Config.ShowAimlines)
                DrawAimline(canvas, pos, aimlinePaint, sin, cos, localPlayer);

            if (!IsLocalPlayer)
            {
                string name = Name;
                string? weaponLine = InHandsItem is not null
                    ? (InHandsAmmo is not null ? $"{InHandsItem} ({InHandsAmmo})" : InHandsItem)
                    : null;

                if (localPlayer is not null)
                {
                    float heightDiff = Position.Y - localPlayer.Position.Y;
                    int h = (int)heightDiff;
                    int d = (int)Vector3.Distance(localPlayer.Position, Position);

                    if (h != _cachedInfoH || d != _cachedInfoD)
                    {
                        _cachedInfoH = h;
                        _cachedInfoD = d;
                        _cachedInfo = string.Create(null, stackalloc char[32], $"{h:+0;-0}m  {d:N0}m");
                    }

                    if (heightDiff > HEIGHT_INDICATOR_THRESHOLD)
                        DrawHeightArrow(canvas, pos, fillPaint, true);
                    else if (heightDiff < -HEIGHT_INDICATOR_THRESHOLD)
                        DrawHeightArrow(canvas, pos, fillPaint, false);

                    DrawLabel(canvas, pos, textPaint, name, _cachedInfo, weaponLine, HealthStatus, GearReady ? GearValue : 0);
                }
                else
                {
                    DrawLabel(canvas, pos, textPaint, name, null, weaponLine, HealthStatus, GearReady ? GearValue : 0);
                }
            }
            else if (IsLocalPlayer)
            {
                DrawLocalPlayerLabel(canvas, pos);
            }
        }

        // Chevron geometry constants
        private const float ChevronTipX = DotRadius + 6f;
        private const float ChevronBaseX = DotRadius + 0.5f;
        private const float ChevronWingY = 3.2f;

        /// <summary>
        /// Draws a filled circle + directional chevron tick.
        /// Manually rotates chevron points to avoid canvas Save/Translate/Rotate/Restore.
        /// </summary>
        private void DrawMarker(SKCanvas canvas, SKPoint point, SKPaint fillPaint, SKPaint chevronPaint, float sin, float cos)
        {
            // Filled dot with thin dark border
            canvas.DrawCircle(point, DotRadius, SKPaints.ShapeBorder);
            canvas.DrawCircle(point, DotRadius - 0.6f, fillPaint);

            // Rotate the 3 chevron vertices manually
            float px = point.X, py = point.Y;

            // Wing top: (ChevronBaseX, -ChevronWingY) rotated + translated
            float w1x = px + cos * ChevronBaseX - sin * (-ChevronWingY);
            float w1y = py + sin * ChevronBaseX + cos * (-ChevronWingY);
            // Tip: (ChevronTipX, 0) rotated + translated
            float tx = px + cos * ChevronTipX;
            float ty = py + sin * ChevronTipX;
            // Wing bottom: (ChevronBaseX, ChevronWingY) rotated + translated
            float w2x = px + cos * ChevronBaseX - sin * ChevronWingY;
            float w2y = py + sin * ChevronBaseX + cos * ChevronWingY;

            // Outline then colored stroke
            canvas.DrawLine(w1x, w1y, tx, ty, _chevronOutline);
            canvas.DrawLine(tx, ty, w2x, w2y, _chevronOutline);
            canvas.DrawLine(w1x, w1y, tx, ty, chevronPaint);
            canvas.DrawLine(tx, ty, w2x, w2y, chevronPaint);
        }

        /// <summary>
        /// Draws a small ▲ or ▼ to the LEFT of the player dot to indicate height difference.
        /// </summary>
        private static void DrawHeightArrow(SKCanvas canvas, SKPoint point, SKPaint fillPaint, bool up)
        {
            const float S = HEIGHT_INDICATOR_ARROW_SIZE;
            float cx = point.X - DotRadius - S - 3f;
            float cy = point.Y;

            using var path = new SKPath();
            if (up)
            {
                path.MoveTo(cx,            cy - S);
                path.LineTo(cx - S * 0.75f, cy + S * 0.6f);
                path.LineTo(cx + S * 0.75f, cy + S * 0.6f);
            }
            else
            {
                path.MoveTo(cx,            cy + S);
                path.LineTo(cx - S * 0.75f, cy - S * 0.6f);
                path.LineTo(cx + S * 0.75f, cy - S * 0.6f);
            }
            path.Close();
            canvas.DrawPath(path, SKPaints.ShapeBorder);
            canvas.DrawPath(path, fillPaint);
        }

        /// <summary>
        /// Draws weapon/ammo label below the local player dot.
        /// </summary>
        private void DrawLocalPlayerLabel(SKCanvas canvas, SKPoint point)
        {
            float x = point.X + DotRadius + 4f;
            float y = point.Y + 4.5f;

            // Name — same style as other players
            canvas.DrawText("LocalPlayer", x + 1, y + 1, SKPaints.FontRegular11, SKPaints.TextShadow);
            canvas.DrawText("LocalPlayer", x,     y,     SKPaints.FontRegular11, SKPaints.TextLocalPlayer);

            string? weapon = InHandsItem is not null
                ? (InHandsAmmo is not null ? $"{InHandsItem} ({InHandsAmmo})" : InHandsItem)
                : null;

            if (weapon is not null)
            {
                float y2 = y + 12f;
                canvas.DrawText(weapon, x + 1, y2 + 1, _localInfoFont, _infoShadow);
                canvas.DrawText(weapon, x,     y2,     _localInfoFont, _localInfoPaint);

                if (GearReady && GearValue > 0)
                {
                    float y3 = y2 + 11f;
                    var valueText = LootFilter.FormatPrice(GearValue);
                    canvas.DrawText(valueText, x + 1, y3 + 1, _localInfoFont, _infoShadow);
                    canvas.DrawText(valueText, x,     y3,     _localInfoFont, _localInfoPaint);
                }
            }
        }

        /// <summary>
        /// Draws a small X for dead players.
        /// </summary>
        private static void DrawDeathMarker(SKCanvas canvas, SKPoint point)
        {
            const float s = 4f;
            float px = point.X, py = point.Y;
            canvas.DrawLine(px - s, py - s, px + s, py + s, SKPaints.PaintDeathMarker);
            canvas.DrawLine(px - s, py + s, px + s, py - s, SKPaints.PaintDeathMarker);
        }

        /// <summary>
        /// Draws an aimline extending from the player dot in the facing direction.
        /// Length varies by player type. High Alert extends the line when the enemy
        /// is aiming at the local player.
        /// </summary>
        private void DrawAimline(SKCanvas canvas, SKPoint point, SKPaint aimlinePaint, float sin, float cos, Player? localPlayer)
        {
            var config = SilkProgram.Config;

            // Base length — shorter for AI, configurable for humans and bosses
            float length = (IsHuman || Type == PlayerType.AIBoss)
                ? config.AimlineLength
                : MathF.Min(config.AimlineLength * 0.5f, 10f);

            // High Alert — extend aimline when hostile is facing local player.
            // Reuses the cached flag from HighAlertManager (updated every realtime tick)
            // so we don't redo the Distance + Normalize + Acos + Log math per frame.
            if (config.HighAlert && (IsHostile || Type == PlayerType.AIBoss) && IsFacingLocalPlayer)
            {
                length = HighAlertLength;
            }

            if (length <= 0f)
                return;

            float startX = point.X + cos * DotRadius;
            float startY = point.Y + sin * DotRadius;
            float endX = point.X + cos * (DotRadius + length);
            float endY = point.Y + sin * (DotRadius + length);

            canvas.DrawLine(startX, startY, endX, endY, _aimlineOutline);
            canvas.DrawLine(startX, startY, endX, endY, aimlinePaint);
        }

        /// <summary>
        /// Draws the player name + optional compact H/D info line + optional weapon/ammo line.
        /// Health status (Injured/Badly Injured/Dying) is shown only when the player is damaged.
        /// </summary>
        private static void DrawLabel(SKCanvas canvas, SKPoint point, SKPaint textPaint, string name, string? info, string? weapon = null, PlayerHealthStatus health = PlayerHealthStatus.Healthy, int gearValue = 0)
        {
            float x = point.X + DotRadius + 4f;
            float y = point.Y + 4.5f;

            // Name — offset shadow then fill for clean contrast
            canvas.DrawText(name, x + 1, y + 1, SKPaints.FontRegular11, SKPaints.TextShadow);
            canvas.DrawText(name, x, y, SKPaints.FontRegular11, textPaint);

            float y2 = y + 12f;

            // Compact H/D on second line in a smaller, dimmer font
            if (info is not null)
            {
                canvas.DrawText(info, x + 1, y2 + 1, _infoFont, _infoShadow);
                canvas.DrawText(info, x, y2, _infoFont, _infoPaint);
                y2 += 11f;
            }

            // Weapon/ammo on next line
            if (weapon is not null)
            {
                canvas.DrawText(weapon, x + 1, y2 + 1, _infoFont, _infoShadow);
                canvas.DrawText(weapon, x, y2, _infoFont, _weaponPaint);
                y2 += 11f;
            }

            // Gear value on next line (only when > 0)
            if (gearValue > 0)
            {
                var valueText = LootFilter.FormatPrice(gearValue);
                canvas.DrawText(valueText, x + 1, y2 + 1, _infoFont, _infoShadow);
                canvas.DrawText(valueText, x,     y2,     _infoFont, _weaponPaint);
                y2 += 11f;
            }

            // Health status — only shown when damaged
            if (health != PlayerHealthStatus.Healthy)
            {
                var (healthPaint, healthText) = health switch
                {
                    PlayerHealthStatus.Injured      => (_healthInjured, "Injured"),
                    PlayerHealthStatus.BadlyInjured => (_healthBadly,   "Badly Injured"),
                    _                               => (_healthDying,   "Dying"),
                };
                canvas.DrawText(healthText, x + 1, y2 + 1, _infoFont, _infoShadow);
                canvas.DrawText(healthText, x,     y2,     _infoFont, healthPaint);
            }
        }

        /// <summary>
        /// Gets the text paint for this player (used by tooltip rendering).
        /// </summary>
        internal SKPaint TextPaint => GetPaints().text;

        /// <summary>
        /// Returns the dot, text, chevron stroke, and aimline stroke paints for this player based on type.
        /// All paints are pre-allocated static instances — never mutated at draw time.
        /// </summary>
        protected virtual (SKPaint dot, SKPaint text, SKPaint chevron, SKPaint aimline) GetPaints()
        {
            return Type switch
            {
                PlayerType.Teammate      => (SKPaints.PaintTeammate, SKPaints.TextTeammate, SKPaints.ChevronTeammate, SKPaints.AimlineTeammate),
                PlayerType.USEC          => (SKPaints.PaintUSEC, SKPaints.TextUSEC, SKPaints.ChevronUSEC, SKPaints.AimlineUSEC),
                PlayerType.BEAR          => (SKPaints.PaintBEAR, SKPaints.TextBEAR, SKPaints.ChevronBEAR, SKPaints.AimlineBEAR),
                PlayerType.PScav         => (SKPaints.PaintPScav, SKPaints.TextPScav, SKPaints.ChevronPScav, SKPaints.AimlinePScav),
                PlayerType.AIScav        => (SKPaints.PaintScav, SKPaints.TextScav, SKPaints.ChevronScav, SKPaints.AimlineScav),
                PlayerType.AIRaider      => (SKPaints.PaintRaider, SKPaints.TextRaider, SKPaints.ChevronRaider, SKPaints.AimlineRaider),
                PlayerType.BtrOperator   => (SKPaints.PaintBtr, SKPaints.TextBtr, SKPaints.ChevronRaider, SKPaints.AimlineRaider),
                PlayerType.AIBoss        => (SKPaints.PaintBoss, SKPaints.TextBoss, SKPaints.ChevronBoss, SKPaints.AimlineBoss),
                PlayerType.SpecialPlayer => (SKPaints.PaintSpecial, SKPaints.TextSpecial, SKPaints.ChevronSpecial, SKPaints.AimlineSpecial),
                PlayerType.Streamer      => (SKPaints.PaintStreamer, SKPaints.TextStreamer, SKPaints.ChevronStreamer, SKPaints.AimlineStreamer),
                _                        => (SKPaints.PaintLocalPlayer, SKPaints.TextLocalPlayer, SKPaints.ChevronLocalPlayer, SKPaints.AimlineLocalPlayer)
            };
        }

        #endregion
    }
}
