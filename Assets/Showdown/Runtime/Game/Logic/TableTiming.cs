namespace Showdown.Game
{
    /// <summary>The beats of a hand, in milliseconds (the constants at the top of src/state/useShowdownTable.ts).</summary>
    public static class TableTiming
    {
        /// <summary>Cards land one per beat; five beats reads as a deal, not a dump.</summary>
        public const int DealBeatMs = 110;

        public const int DealCardCount = 5;

        public const int DealSettleMs = 360;

        /// <summary>The pause before each dealer card turns — the climb's heartbeat.</summary>
        public const int DealerCardBeatMs = 650;

        /// <summary>Each bot visibly considers before acting — thought, not a stall.</summary>
        public const int BotThinkBaseMs = 420;

        public const int BotThinkStepMs = 110;

        /// <summary>Bot think time varies with <c>seatIndex % BotThinkSeatCycle</c>, so the regulars do not move in lockstep.</summary>
        public const int BotThinkSeatCycle = 3;

        /// <summary>The pause between the fifth card and the table's hands turning over.</summary>
        public const int RevealBeatMs = 650;

        /// <summary>The showdown sits face-up for a breath before chips move.</summary>
        public const int SettleBeatMs = 700;

        public const int BustCueDelayMs = 800;

        /// <summary>
        /// How long a freshly forged card stays ringed. Generous on purpose: this table is played by people in
        /// their sixties and up, and a mark that has faded before the eye reaches it may as well not have been drawn.
        /// </summary>
        public const int FreshCardMs = 1800;

        public const float MillisecondsPerSecond = 1000f;

        public static float ToSeconds(int milliseconds) => milliseconds / MillisecondsPerSecond;
    }
}
