namespace eft_dma_radar.Silk.Tarkov.GameWorld.Player
{
    /// <summary>
    /// Player type classification — determines radar color, draw priority, and hostility.
    /// </summary>
    public enum PlayerType
    {
        /// <summary>Unclassified / fallback.</summary>
        Default,
        /// <summary>Same-group teammate (not drawn as hostile).</summary>
        Teammate,
        /// <summary>USEC PMC.</summary>
        USEC,
        /// <summary>BEAR PMC.</summary>
        BEAR,
        /// <summary>AI-controlled scav.</summary>
        AIScav,
        /// <summary>AI raider (e.g. labs, reserve).</summary>
        AIRaider,
        /// <summary>AI boss (Killa, Reshala, etc.).</summary>
        AIBoss,
        /// <summary>Player-controlled scav.</summary>
        PScav,
        /// <summary>Special player (dev, sherpa, etc.).</summary>
        SpecialPlayer,
        /// <summary>Known streamer.</summary>
        Streamer,
        /// <summary>BTR turret operator (AI).</summary>
        BtrOperator
    }

    internal static class PlayerTypeExtensions
    {
        /// <summary>Returns true for human-controlled player types whose real names should be hidden in streamer mode.</summary>
        public static bool IsHumanPlayer(this PlayerType type) => type is
            PlayerType.USEC or
            PlayerType.BEAR or
            PlayerType.PScav or
            PlayerType.Teammate or
            PlayerType.SpecialPlayer or
            PlayerType.Streamer;

        /// <summary>Returns a faction label used in the killfeed when streamer mode is active.</summary>
        public static string StreamerLabel(this PlayerType type) => type switch
        {
            PlayerType.USEC          => "Usec",
            PlayerType.BEAR          => "Bear",
            PlayerType.PScav         => "PScav",
            PlayerType.Teammate      => "Teammate",
            PlayerType.SpecialPlayer => "Special",
            PlayerType.Streamer      => "Streamer",
            _                        => type.ToString(),
        };
    }
}
