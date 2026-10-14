using Showdown.Domain;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The app shell (App.tsx): which screen is showing, the cross-fade between them, Android back handling, and the
    /// "leave this hand?" question.
    ///
    /// There is no navigation stack: four screens and one cross-fade is not a navigation problem, and a stack would
    /// only add a back gesture the felt does not want. The shell reads the table, settings and daily-bonus stores
    /// through <see cref="AppServices"/> and rebuilds the screen when the table says something navigation-relevant changed.
    /// </summary>
    public sealed class AppRoot : INavigator
    {
        private readonly ScreenTransition transition = new ScreenTransition();

        /// <summary>The screen asked for. It can differ from what is showing: a Table request with no hand dealt shows Home.</summary>
        private ScreenName requested = ScreenName.Home;
        private ScreenName? displayed;

        public ScreenName Current => displayed ?? requested;

        public AppRoot(VisualElement parent)
        {
            parent.Add(transition);
            if (!ServicesReady())
            {
                Debug.LogError("Showdown: the game layer has not published its services; showing the felt only.");
                return;
            }

            AppServices.Table.Changed += OnTableChanged;
            UiHost.Frame += OnFrame;
            Render();
        }

        public void Go(ScreenName screen)
        {
            requested = screen;
            Render();
        }

        private static bool ServicesReady()
        {
            return AppServices.Table != null && AppServices.Settings != null && AppServices.DailyBonus != null;
        }

        private void OnTableChanged() => Render();

        // ---- what is showing -----------------------------------------------------------------------

        private void Render()
        {
            var table = AppServices.Table;
            // Until the stored bankroll lands, show only the felt: opening on the default 500 and then snapping
            // to the real number reads as a bug.
            if (!table.BankrollLoaded)
            {
                if (displayed.HasValue) transition.ShowNothing();
                displayed = null;
                return;
            }

            // A table screen with no round loaded has nothing to draw — that only happens if the deal was
            // refused, and home is the honest place to be.
            var target = requested == ScreenName.Table && table.Round == null ? ScreenName.Home : requested;
            if (displayed == target) return;

            displayed = target;
            transition.Show(Build(target));
        }

        private VisualElement Build(ScreenName screen)
        {
            var table = AppServices.Table;
            switch (screen)
            {
                case ScreenName.HowToPlay:
                    return new HowToPlayScreen(() => Go(ReturnScreen()));
                case ScreenName.Stats:
                    return new StatsScreen(table.Stats, () => Go(ReturnScreen()));
                case ScreenName.Settings:
                    return new SettingsScreen(AppServices.Settings, ResetBankroll, () => Go(ReturnScreen()));
                case ScreenName.Table:
                    return new TableScreen(table, LeaveTable, () => Go(ScreenName.HowToPlay), () => Go(ScreenName.Settings));
                default:
                    return new HomeScreen(table, AppServices.Settings, AppServices.DailyBonus, HomeActions());
            }
        }

        private HomeActions HomeActions()
        {
            return new HomeActions
            {
                Play = TakeSeat,
                ReUp = () => AppServices.Table.ReUpNow(AppServices.Settings.Current.Ante),
                HowToPlay = () => Go(ScreenName.HowToPlay),
                Settings = () => Go(ScreenName.Settings),
                Stats = () => Go(ScreenName.Stats),
                ClaimDailyBonus = ClaimDailyBonus,
            };
        }

        // ---- navigation rules ----------------------------------------------------------------------

        /// <summary>Rules and settings are reachable from both places, so back goes wherever you were.</summary>
        private static ScreenName ReturnScreen()
        {
            return AppServices.Table.Round != null ? ScreenName.Table : ScreenName.Home;
        }

        private void TakeSeat()
        {
            AppServices.Table.StartHand(AppServices.Settings.Current.Ante);
            Go(ScreenName.Table);
        }

        private void QuitToHome()
        {
            AppServices.Table.LeaveTable();
            Go(ScreenName.Home);
        }

        /// <summary>
        /// Nothing is banked until a hand settles, so walking out costs no chips — but it does throw away a hand in
        /// progress, and the home button sits a thumb away from the rules button. Worth one question; not worth a
        /// warning that implies a loss that does not happen.
        /// </summary>
        private void LeaveTable()
        {
            var round = AppServices.Table.Round;
            if (round == null || round.Phase == ShowdownPhase.Settled)
            {
                QuitToHome();
                return;
            }
            ConfirmDialog.Show(
                "Leave this hand?",
                "This hand will not be played out. Nothing has been settled, so your chips are untouched.",
                "Stay", "Leave", QuitToHome);
        }

        private void ResetBankroll()
        {
            AppServices.Table.LeaveTable();
            AppServices.Table.ResetBankroll();
            Go(ScreenName.Home);
        }

        private static void ClaimDailyBonus()
        {
            if (!AppServices.DailyBonus.CanClaim) return;
            AppServices.Table.ClaimDailyBonus();
            AppServices.DailyBonus.MarkClaimed();
        }

        // ---- Android back --------------------------------------------------------------------------

        private void OnFrame(float deltaSeconds)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        /// <summary>An open question closes first; then back does what the on-screen back (or home) button does. On Home it leaves the app.</summary>
        private void HandleBack()
        {
            if (ConfirmDialog.DismissOpen()) return;
            switch (displayed)
            {
                case ScreenName.Table:
                    LeaveTable();
                    return;
                case ScreenName.HowToPlay:
                case ScreenName.Stats:
                case ScreenName.Settings:
                    Go(ReturnScreen());
                    return;
                default:
                    Application.Quit();
                    return;
            }
        }
    }
}
