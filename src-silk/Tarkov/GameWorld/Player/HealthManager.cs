using static SDK.Offsets;

namespace eft_dma_radar.Silk.Tarkov.GameWorld.Player
{
    /// <summary>
    /// Reads health data from memory and updates player health properties.
    /// Called from the registration worker alongside gear/hands refresh.
    /// </summary>
    internal static class HealthManager
    {
        // ETagStatus flag values from SDK_Manual.cs
        private const int ETagHealthy      = 1024;
        private const int ETagInjured      = 2048;
        private const int ETagBadlyInjured = 4096;
        private const int ETagDying        = 8192;

        /// <summary>
        /// Refreshes health status for observed players only.
        /// Local player does not use ETagStatus.
        /// </summary>
        internal static void Refresh(ulong playerBase, Player player, bool isObserved)
        {
            if (isObserved)
                RefreshObserved(playerBase, player);
        }

        /// <summary>
        /// Observed player: reads ETagStatus int and derives a <see cref="PlayerHealthStatus"/>.
        /// Chain: playerBase → ObservedPlayerController → HealthController → HealthStatus (int32)
        /// </summary>
        private static void RefreshObserved(ulong playerBase, Player player)
        {
            try
            {
                if (!Memory.TryReadPtr(playerBase + ObservedPlayerView.ObservedPlayerController, out var opc, false) || opc == 0)
                    return;

                if (!Memory.TryReadPtr(opc + ObservedPlayerController.HealthController, out var hc, false) || hc == 0)
                    return;

                if (!Memory.TryReadValue<int>(hc + ObservedHealthController.HealthStatus, out var tag, false))
                    return;

                player.HealthStatus =
                    (tag & ETagDying)        == ETagDying        ? PlayerHealthStatus.Dying        :
                    (tag & ETagBadlyInjured) == ETagBadlyInjured ? PlayerHealthStatus.BadlyInjured :
                    (tag & ETagInjured)      == ETagInjured      ? PlayerHealthStatus.Injured      :
                                                                   PlayerHealthStatus.Healthy;
            }
            catch
            {
                // non-critical — leave last known value in place
            }
        }
    }
}
