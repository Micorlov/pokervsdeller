using System;
using System.Collections.Generic;
using Showdown.Domain;

namespace Showdown.Game
{
    // The seam between the game layer (state, clock, storage, sound) and the UI. The UI reads these
    // interfaces and never touches storage or timers; the game layer implements them. Port of the hooks in
    // src/state/*.ts and src/feedback/*.ts.

    /// <summary>Every sound-and-buzz the app can make (src/feedback/cues.ts).</summary>
    public enum Cue
    {
        Deal,
        Flip,
        Chip,
        Raise,
        Fold,
        Reveal,
        Win,
        WinBig,
        Lose,
        Push,
        Bust,
        Tap,
    }

    /// <summary>What <c>useFeedback().play</c> was: one call per event, sound and haptic together.</summary>
    public interface IFeedback
    {
        void Play(Cue cue);
    }

    /// <summary>Player preferences (src/state/settingsStore.ts). Immutable; change with <c>with</c>.</summary>
    public sealed record AppSettings
    {
        public bool SoundOn { get; init; } = true;
        public bool HapticsOn { get; init; } = true;
        public bool ReducedMotion { get; init; }
        /// <summary>The table stake the player last chose — remembered between sessions.</summary>
        public int Ante { get; init; } = Bankroll.AnteOptions[1];
    }

    public interface ISettingsStore
    {
        AppSettings Current { get; }
        bool IsLoaded { get; }
        /// <summary>Raised after <see cref="Current"/> changes (including the initial load).</summary>
        event Action Changed;
        void Update(Func<AppSettings, AppSettings> patch);
    }

    /// <summary>Eligibility is derived from today's date on every read, so a session left open overnight can claim again.</summary>
    public interface IDailyBonusStore
    {
        bool CanClaim { get; }
        event Action Changed;
        void MarkClaimed();
    }

    /// <summary>The win-streak payout a settled hand just made (e.g. streak 3 pays 2 antes).</summary>
    public sealed record StreakPayout(int Streak, int Amount);

    /// <summary>
    /// Owns the table and the clock (src/state/useShowdownTable.ts). The domain settles a hand instantly; this
    /// spreads that instant across the beats the eye needs — deal, pick a kicker, watch the climb, act, reveal,
    /// pay — and fires the matching cue at each. The UI redraws whenever <see cref="Changed"/> fires.
    /// </summary>
    public interface ITableController
    {
        event Action Changed;

        /// <summary>The hand in progress, or null when the player is not at the table.</summary>
        ShowdownRoundState Round { get; }
        /// <summary>Index into <c>Round.Seats</c> of the bot currently "thinking", or null.</summary>
        int? ThinkingSeat { get; }
        /// <summary>True once the human has locked a kicker and the climb is under way.</summary>
        bool KickerLocked { get; }
        /// <summary>True while the human is picking which card a forge trades away.</summary>
        bool ForgeArming { get; }
        /// <summary>The hand slot a forge just refilled, held long enough to be noticed.</summary>
        int? FreshCardIndex { get; }
        /// <summary>The pressure toggle, folded into the human's next move in round three.</summary>
        bool PressureArmed { get; }
        /// <summary>Flips the table's hidden hands on the reveal beat, before the settle.</summary>
        bool Revealed { get; }
        /// <summary>Badges the hand that just settled newly earned, cleared on dismiss or the next deal.</summary>
        IReadOnlyList<Achievement> NewAchievements { get; }
        /// <summary>The win-streak bonus the hand that just settled paid, or null.</summary>
        StreakPayout StreakBonus { get; }
        /// <summary>During a hand the human seat's stack IS the bankroll — there is no second ledger.</summary>
        int Bankroll { get; }
        bool BankrollLoaded { get; }
        Statistics Stats { get; }

        void StartHand(int ante);
        void SelectKicker(int cardIndex);
        void LockKicker();
        void Hold();
        void Forge(int discardIndex);
        void Bail();
        void BeginForge();
        void CancelForge();
        void SetPressureArmed(bool armed);
        void NextHand();
        /// <summary>The free top-up; a no-op unless the stack really is short. <paramref name="atAnte"/> is only read away from the table.</summary>
        void ReUpNow(int atAnte);
        void ClaimDailyBonus();
        void DismissAchievements();
        void DismissStreakBonus();
        /// <summary>Leaving mid-hand just abandons the presentation — nothing was settled.</summary>
        void LeaveTable();
        void ResetBankroll();

        /// <summary>QA only: the next dealt hand uses this RNG seed, so a screenshot scenario is repeatable.</summary>
        void PinNextSeed(uint seed);
    }

    public enum ScreenName
    {
        Home,
        Table,
        HowToPlay,
        Stats,
        Settings,
    }

    /// <summary>Screen switching, implemented by the app shell (AppRoot). QA scenarios drive the app through it.</summary>
    public interface INavigator
    {
        ScreenName Current { get; }
        void Go(ScreenName screen);
    }

    /// <summary>The app's services, created once by <see cref="ShowdownApp"/> and read by the UI.</summary>
    public static class AppServices
    {
        public static IFeedback Feedback { get; set; }
        public static ISettingsStore Settings { get; set; }
        public static IDailyBonusStore DailyBonus { get; set; }
        public static ITableController Table { get; set; }
        public static INavigator Navigator { get; set; }

        /// <summary>Plays a cue if feedback is up; the UI calls this so it never needs a null check.</summary>
        public static void Play(Cue cue)
        {
            Feedback?.Play(cue);
        }
    }
}
