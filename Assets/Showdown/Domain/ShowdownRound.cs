#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    public enum ShowdownPhase
    {
        Ante,
        Dealing,
        Kicker,
        Actions,
        Reveal,
        Settled,
    }

    public enum SeatStatus
    {
        Active,
        Bailed,
    }

    public sealed record ShowdownSeatState : IShowdownBotSeat
    {
        public string Id { get; init; } = "";

        public string Name { get; init; } = "";

        public bool IsHuman { get; init; }

        public BotAvatar Avatar { get; init; } = new BotAvatar();

        /// <summary>Null for the human seat — only bots are played by a persona.</summary>
        public BotPersona? Persona { get; init; }

        /// <summary>Chips in front of the seat. Everything committed has already left it.</summary>
        public int Stack { get; init; }

        public IReadOnlyList<Card> Cards { get; init; } = Array.Empty<Card>();

        /// <summary>Which card is the kicker. Set during the kicker phase, then immutable.</summary>
        public int? KickerIndex { get; init; }

        public int ForgesUsed { get; init; }

        public bool Pressured { get; init; }

        /// <summary>Chips this seat has put into the table pot.</summary>
        public int TableContrib { get; init; }

        /// <summary>Chips this seat has staked against the house.</summary>
        public int DealerStake { get; init; }

        public SeatStatus Status { get; init; }

        /// <summary>The half-back a bail already returned to the stack. Zero otherwise.</summary>
        public int Refund { get; init; }

        /// <summary>This action round's move, cleared when the next dealer card lands.</summary>
        public ShowdownMove? LastMove { get; init; }

        public ShowdownSettlement? Settlement { get; init; }
    }

    public sealed record ShowdownRoundState
    {
        public ShowdownPhase Phase { get; init; }

        public int HandNumber { get; init; }

        public int Ante { get; init; }

        public IReadOnlyList<ShowdownSeatState> Seats { get; init; } = Array.Empty<ShowdownSeatState>();

        /// <summary>The dealer's climb, all face up, oldest first.</summary>
        public IReadOnlyList<Card> DealerCards { get; init; } = Array.Empty<Card>();

        /// <summary>The undealt remainder — dealer draws and forge replacements both come from here.</summary>
        public IReadOnlyList<Card> Stock { get; init; } = Array.Empty<Card>();

        public int TablePot { get; init; }

        /// <summary>Chips the house kept (positive) or paid out (negative). Bail refunds land here mid-hand.</summary>
        public int HouseNet { get; init; }
    }

    /// <summary>
    /// One hand of Showdown, as a set of pure reducers.
    ///
    /// The shape mirrors the old engine deliberately: no timers, no presentation
    /// state, every chip movement in exactly one function. What is new is the
    /// clock inside the hand — the dealer's cards arrive one at a time, and the
    /// table acts between them, so the phase loops kicker, actions, actions,
    /// ... reveal instead of running straight through.
    /// Every reducer returns a new state; the input is never modified, and a
    /// reducer that has nothing to do returns the very same instance.
    /// </summary>
    public static class ShowdownRound
    {
        /// <summary>Two regulars and you — derived so the roster and the felt cannot drift.</summary>
        public static int BotCount => Bots.BotPersonas.Count;

        public static int SeatCount => BotCount + 1;

        /// <summary>The human always sits in the first seat. (<c>HUMAN_SEAT</c> in the original; <see cref="HumanSeat"/> is the accessor.)</summary>
        public const int HumanSeatIndex = 0;

        /// <summary>Bots keep a session stack and quietly buy back in when it runs dry.</summary>
        public const int BotStartingStack = 500;

        private static ShowdownSeatState ClearedSeat(ShowdownSeatState seat) =>
            seat with
            {
                Cards = Array.Empty<Card>(),
                KickerIndex = null,
                ForgesUsed = 0,
                Pressured = false,
                TableContrib = 0,
                DealerStake = 0,
                Status = SeatStatus.Active,
                Refund = 0,
                LastMove = null,
                Settlement = null,
            };

        private static IReadOnlyList<ShowdownSeatState> MapSeat(
            IReadOnlyList<ShowdownSeatState> seats,
            int seatIndex,
            Func<ShowdownSeatState, ShowdownSeatState> update) =>
            seats.Select((seat, index) => index == seatIndex ? update(seat) : seat).ToArray();

        private static ShowdownSeatState SeatAt(ShowdownRoundState state, int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= state.Seats.Count)
            {
                throw new ArgumentException($"No seat at index {seatIndex}");
            }

            return state.Seats[seatIndex];
        }

        /// <summary>The human's seat, plus the two regulars, before any cards are out.</summary>
        public static ShowdownRoundState CreateShowdownTable(int humanStack, int ante, string humanName = "You")
        {
            var seats = new List<ShowdownSeatState>
            {
                ClearedSeat(new ShowdownSeatState
                {
                    Id = "you",
                    Name = humanName,
                    IsHuman = true,
                    Avatar = new BotAvatar
                    {
                        Kind = BotAvatarKind.Initial,
                        Value = humanName.Length > 0 ? humanName.Substring(0, 1).ToUpperInvariant() : "",
                    },
                    Persona = null,
                    Stack = humanStack,
                }),
            };

            foreach (var persona in Bots.BotPersonas)
            {
                seats.Add(ClearedSeat(new ShowdownSeatState
                {
                    Id = persona.Id,
                    Name = persona.Name,
                    IsHuman = false,
                    Avatar = persona.Avatar,
                    Persona = persona,
                    Stack = BotStartingStack,
                }));
            }

            return new ShowdownRoundState
            {
                Phase = ShowdownPhase.Ante,
                HandNumber = 0,
                Ante = ante,
                Seats = seats,
                DealerCards = Array.Empty<Card>(),
                Stock = Array.Empty<Card>(),
                TablePot = 0,
                HouseNet = 0,
            };
        }

        /// <summary>
        /// Antes everyone up and deals every player hand.
        ///
        /// Both antes leave every stack at once — one chip stack into the table pot,
        /// one staked against the house. The dealer gets nothing yet; the rest of the
        /// shuffled deck is kept as the stock the climb and every forge draw from.
        /// Pass <paramref name="ante"/> to change the stakes; it defaults to the table's.
        /// </summary>
        public static ShowdownRoundState StartShowdownRound(ShowdownRoundState state, Func<double> rng, int? ante = null)
        {
            var stake = ante ?? state.Ante;
            var rebought = state.Seats
                .Select(seat =>
                    !seat.IsHuman && seat.Stack < Bankroll.MinimumToPlay(stake)
                        ? ClearedSeat(seat) with { Stack = BotStartingStack }
                        : ClearedSeat(seat))
                .ToList();

            var shortSeat = rebought.FirstOrDefault(seat => seat.Stack < stake * 2);
            if (shortSeat != null)
            {
                throw new InvalidOperationException(
                    $"{shortSeat.Name} cannot cover the two {stake} antes with {shortSeat.Stack}");
            }

            var deck = Rng.Shuffle(Cards.CreateDeck(), rng);
            var hands = Enumerable.Range(0, SeatCount).Select(_ => new List<Card>()).ToArray();
            var cursor = 0;
            for (var pass = 0; pass < HandRanks.HandSize; pass += 1)
            {
                for (var seat = 0; seat < SeatCount; seat += 1)
                {
                    hands[seat].Add(deck[cursor]);
                    cursor += 1;
                }
            }

            return new ShowdownRoundState
            {
                Phase = ShowdownPhase.Dealing,
                HandNumber = state.HandNumber + 1,
                Ante = stake,
                HouseNet = 0,
                DealerCards = Array.Empty<Card>(),
                Stock = deck.Skip(cursor).ToArray(),
                TablePot = stake * SeatCount,
                Seats = rebought
                    .Select((seat, index) => seat with
                    {
                        Stack = seat.Stack - stake * 2,
                        TableContrib = stake,
                        DealerStake = stake,
                        Cards = hands[index],
                    })
                    .ToArray(),
            };
        }

        /// <summary>The last card has landed; everyone picks a kicker before the climb starts.</summary>
        public static ShowdownRoundState FinishDealing(ShowdownRoundState state) =>
            state.Phase == ShowdownPhase.Dealing ? state with { Phase = ShowdownPhase.Kicker } : state;

        /// <summary>Marks one card as the seat's kicker. Re-picking is fine until the climb starts.</summary>
        public static ShowdownRoundState SetKicker(ShowdownRoundState state, int seatIndex, int cardIndex)
        {
            var seat = SeatAt(state, seatIndex);
            if (state.Phase != ShowdownPhase.Kicker) return state;
            if (cardIndex < 0 || cardIndex >= seat.Cards.Count)
            {
                throw new ArgumentException($"Kicker index {cardIndex} is outside the hand");
            }

            return state with { Seats = MapSeat(state.Seats, seatIndex, current => current with { KickerIndex = cardIndex }) };
        }

        public static bool AllKickersSet(ShowdownRoundState state) =>
            state.Seats.All(seat => seat.KickerIndex != null);

        /// <summary>Which action round the table is in: the number of dealer cards showing.</summary>
        public static int CurrentActionRound(ShowdownRoundState state) => state.DealerCards.Count;

        public static bool AllActed(ShowdownRoundState state) =>
            state.Seats.All(seat => seat.Status == SeatStatus.Bailed || seat.LastMove != null);

        /// <summary>
        /// The dealer draws.
        ///
        /// Legal from the kicker phase once every kicker is locked, and from an action
        /// round once every active seat has moved. The fifth card ends the climb and
        /// opens the reveal; every earlier card opens a fresh action round.
        /// </summary>
        public static ShowdownRoundState DealDealerCard(ShowdownRoundState state)
        {
            if (state.Phase != ShowdownPhase.Kicker && state.Phase != ShowdownPhase.Actions) return state;
            if (state.Phase == ShowdownPhase.Kicker && !AllKickersSet(state))
            {
                throw new InvalidOperationException("The dealer cannot draw before every kicker is set");
            }

            if (state.Phase == ShowdownPhase.Actions && !AllActed(state))
            {
                throw new InvalidOperationException("The dealer cannot draw while seats still owe a move");
            }

            if (state.Stock.Count == 0)
            {
                throw new InvalidOperationException("The stock ran dry mid-climb");
            }

            var dealerCards = state.DealerCards.Concat(new[] { state.Stock[0] }).ToArray();
            var stock = state.Stock.Skip(1).ToArray();

            if (dealerCards.Length == HandRanks.HandSize)
            {
                return state with { DealerCards = dealerCards, Stock = stock, Phase = ShowdownPhase.Reveal };
            }

            return state with
            {
                DealerCards = dealerCards,
                Stock = stock,
                Phase = ShowdownPhase.Actions,
                Seats = state.Seats
                    .Select(seat => seat.Status == SeatStatus.Active ? seat with { LastMove = null } : seat)
                    .ToArray(),
            };
        }

        private static void ValidateMove(ShowdownRoundState state, ShowdownSeatState seat, ShowdownMove move)
        {
            if (move.Pressure && move.Kind == ShowdownMoveKind.Bail)
            {
                throw new InvalidOperationException("A bail cannot carry pressure");
            }

            if (move.Pressure && CurrentActionRound(state) != ShowdownPayouts.PressureRound)
            {
                throw new InvalidOperationException(
                    $"Pressure is only legal after the dealer's card {ShowdownPayouts.PressureRound}");
            }

            if (move.Kind == ShowdownMoveKind.Forge)
            {
                var discard = move.ForgeDiscardIndex;
                if (discard == null || discard.Value < 0 || discard.Value >= seat.Cards.Count)
                {
                    throw new InvalidOperationException("A forge needs a card to trade away");
                }

                if (discard.Value == seat.KickerIndex)
                {
                    throw new InvalidOperationException("The kicker cannot be forged away");
                }

                if (seat.ForgesUsed >= ShowdownPayouts.ForgeLimit)
                {
                    throw new InvalidOperationException($"Seat {seat.Id} has no forges left");
                }

                if (state.Stock.Count == 0)
                {
                    throw new InvalidOperationException("The stock ran dry mid-forge");
                }
            }

            var cost = (move.Kind == ShowdownMoveKind.Forge ? state.Ante : 0) + (move.Pressure ? state.Ante : 0);
            if (seat.Stack < cost)
            {
                throw new InvalidOperationException($"Seat {seat.Id} cannot cover a {cost} move with {seat.Stack}");
            }
        }

        /// <summary>
        /// Records one seat's move for the current action round, moving chips as it
        /// goes: a forge fee sweetens the table pot, pressure doubles the dealer
        /// stake, and a bail hands half of everything committed straight back.
        /// </summary>
        public static ShowdownRoundState ApplyMove(ShowdownRoundState state, int seatIndex, ShowdownMove move)
        {
            var seat = SeatAt(state, seatIndex);
            if (state.Phase != ShowdownPhase.Actions) return state;
            if (seat.Status == SeatStatus.Bailed || seat.LastMove != null) return state;

            ValidateMove(state, seat, move);

            if (move.Kind == ShowdownMoveKind.Bail)
            {
                var refund = ShowdownPayouts.BailRefund(seat.TableContrib + seat.DealerStake);

                // The house pays the half-back; the abandoned dealer stake pours into the
                // table pot on the spot, so the pot grows every time someone quits.
                return state with
                {
                    HouseNet = state.HouseNet - refund,
                    TablePot = state.TablePot + seat.DealerStake,
                    Seats = MapSeat(state.Seats, seatIndex, current => current with
                    {
                        Status = SeatStatus.Bailed,
                        Refund = refund,
                        Stack = current.Stack + refund,
                        LastMove = move,
                    }),
                };
            }

            var isForge = move.Kind == ShowdownMoveKind.Forge;
            var pressureCost = move.Pressure ? state.Ante : 0;
            var forgeCost = isForge ? state.Ante : 0;

            return state with
            {
                Stock = isForge ? state.Stock.Skip(1).ToArray() : state.Stock,
                TablePot = state.TablePot + forgeCost,
                Seats = MapSeat(state.Seats, seatIndex, current => current with
                {
                    Cards = isForge
                        ? current.Cards.Select((card, index) => index == move.ForgeDiscardIndex ? state.Stock[0] : card).ToArray()
                        : current.Cards,
                    ForgesUsed = isForge ? current.ForgesUsed + 1 : current.ForgesUsed,
                    Stack = current.Stack - forgeCost - pressureCost,
                    TableContrib = current.TableContrib + forgeCost,
                    DealerStake = current.DealerStake + pressureCost,
                    Pressured = current.Pressured || move.Pressure,
                    LastMove = move,
                }),
            };
        }

        /// <summary>The dealer's finished hand, or null while the climb is still on.</summary>
        public static HandRank? DealerRankOf(ShowdownRoundState state) =>
            state.DealerCards.Count == HandRanks.HandSize ? HandRanks.EvaluateHand(state.DealerCards) : null;

        /// <summary>
        /// Pays the table out on both fronts.
        ///
        /// Survivors get back their stakes and contributions plus their settlement
        /// net; bailed seats were paid on the way out and get nothing more. The house
        /// ledger picks up the settle-step delta on top of the refunds it already paid.
        /// </summary>
        public static ShowdownRoundState SettleShowdown(ShowdownRoundState state)
        {
            if (state.Phase != ShowdownPhase.Reveal) return state;
            var dealerRank = DealerRankOf(state);
            if (dealerRank == null || state.DealerCards.Count < HandRanks.HandSize)
            {
                throw new InvalidOperationException("Cannot settle before the climb is finished");
            }

            var finalCard = state.DealerCards[HandRanks.HandSize - 1];

            var inputs = state.Seats.Select(seat =>
            {
                if (seat.KickerIndex == null)
                {
                    throw new InvalidOperationException($"Seat {seat.Id} reached the settle without a kicker");
                }

                var bailed = seat.Status == SeatStatus.Bailed;
                return new SettleInput
                {
                    Rank = bailed ? null : HandRanks.EvaluateHand(seat.Cards),
                    KickerRank = seat.Cards[seat.KickerIndex.Value].Rank,
                    DealerStake = seat.DealerStake,
                    TableContrib = seat.TableContrib,
                    Pressured = seat.Pressured,
                    Bailed = bailed,
                    Refund = seat.Refund,
                };
            }).ToArray();

            var result = ShowdownPayouts.SettleSeats(inputs, dealerRank, finalCard, state.TablePot, state.Ante);

            return state with
            {
                Phase = ShowdownPhase.Settled,
                TablePot = 0,
                HouseNet = state.HouseNet + result.HouseDelta,
                Seats = state.Seats.Select((seat, index) =>
                {
                    var settlement = result.Settlements[index];
                    var returned = seat.Status == SeatStatus.Bailed
                        ? 0
                        : settlement.Net + seat.DealerStake + seat.TableContrib;
                    return seat with { Settlement = settlement, Stack = seat.Stack + returned };
                }).ToArray(),
            };
        }

        public static ShowdownSeatState HumanSeat(ShowdownRoundState state) => state.Seats[HumanSeatIndex];

        public static IReadOnlyList<ShowdownSeatState> ActiveSeats(ShowdownRoundState state) =>
            state.Seats.Where(seat => seat.Status == SeatStatus.Active).ToArray();
    }
}
