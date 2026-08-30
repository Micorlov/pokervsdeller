import { useCallback, useEffect, useRef, useState } from 'react';
import { Achievement, newlyUnlockedAchievements } from '../domain/achievements';
import { Ante, canPlay, isBust, reUp } from '../domain/bankroll';
import { chooseBotKicker, decideShowdownMove } from '../domain/bots';
import { DAILY_BONUS_AMOUNT } from '../domain/dailyBonus';
import { evaluateHand } from '../domain/handRank';
import { Rng, mulberry32, randomSeed } from '../domain/rng';
import {
  HUMAN_SEAT,
  SEAT_COUNT,
  ShowdownRoundState,
  allActed,
  allKickersSet,
  applyMove,
  createShowdownTable,
  dealDealerCard,
  finishDealing,
  humanSeat,
  setKicker,
  settleShowdown,
  startShowdownRound,
} from '../domain/showdown';
import { PRESSURE_ROUND, ShowdownMove } from '../domain/showdownPayouts';
import { outcomeOf, recordHandResult } from '../domain/stats';
import { useFeedback } from '../feedback/FeedbackProvider';
import { useBankroll } from './bankrollStore';
import { useStats } from './statsStore';

/** Cards land one per beat; five beats reads as a deal, not a dump. */
const DEAL_BEAT_MS = 110;
const DEAL_SETTLE_MS = 360;
/** The pause before each dealer card turns — the climb's heartbeat. */
const DEALER_CARD_BEAT_MS = 650;
/** Each bot visibly considers before acting — thought, not a stall. */
const BOT_THINK_BASE_MS = 420;
const BOT_THINK_STEP_MS = 110;
/** The pause between the fifth card and the table's hands turning over. */
const REVEAL_BEAT_MS = 650;
/** The showdown sits face-up for a breath before chips move. */
const SETTLE_BEAT_MS = 700;
const BUST_CUE_DELAY_MS = 800;

/**
 * Owns the table and the clock. The domain settles a hand instantly; this
 * hook spreads that instant across the beats the eye needs — deal, pick a
 * kicker, watch the climb, act, reveal, pay — and fires the matching cue at
 * each one.
 */
export const useShowdownTable = () => {
  const { play } = useFeedback();
  const { bankroll, isLoaded: bankrollLoaded, save: saveBankroll, reset: resetBankroll } = useBankroll();
  const { stats, update: updateStats } = useStats();

  const [round, setRoundState] = useState<ShowdownRoundState | null>(null);
  const [thinkingSeat, setThinkingSeat] = useState<number | null>(null);
  /** True once the human has locked a kicker and the climb is under way. */
  const [kickerLocked, setKickerLocked] = useState(false);
  /** True while the human is picking which card a forge trades away. */
  const [forgeArming, setForgeArming] = useState(false);
  /** The pressure toggle, folded into the human's next move in round three. */
  const [pressureArmed, setPressureArmed] = useState(false);
  /** Flips the table's hidden hands in the UI on the reveal beat, not at settle. */
  const [revealed, setRevealed] = useState(false);
  /** Badges the hand that just settled newly earned, cleared on dismiss or the next deal. */
  const [newAchievements, setNewAchievements] = useState<readonly Achievement[]>([]);

  const roundRef = useRef<ShowdownRoundState | null>(null);
  const rngRef = useRef<Rng>(mulberry32(randomSeed()));
  const timersRef = useRef<ReturnType<typeof setTimeout>[]>([]);
  const settleScheduledRef = useRef(false);
  /** The next dealer card already on the clock, so a round schedules one draw. */
  const drawScheduledForRef = useRef(0);

  const setRound = useCallback((next: ShowdownRoundState | null) => {
    roundRef.current = next;
    setRoundState(next);
  }, []);

  const schedule = useCallback((delay: number, fn: () => void) => {
    const id = setTimeout(fn, delay);
    timersRef.current.push(id);
  }, []);

  const clearTimers = useCallback(() => {
    timersRef.current.forEach(clearTimeout);
    timersRef.current = [];
  }, []);

  useEffect(() => clearTimers, [clearTimers]);

  /**
   * Bots act one at a time around the table; the human can act at any point.
   *
   * The whole cascade is laid out on the clock up front rather than each seat
   * calling the next: a self-referencing callback would close over a stale
   * copy of itself, and one seat's timer failing would silently strand the
   * rest of the table.
   */
  const runBotTurns = useCallback(() => {
    let elapsed = 0;
    for (let seatIndex = HUMAN_SEAT + 1; seatIndex < SEAT_COUNT; seatIndex += 1) {
      const startsAt = elapsed;
      elapsed += BOT_THINK_BASE_MS + (seatIndex % 3) * BOT_THINK_STEP_MS;
      schedule(startsAt, () => {
        const current = roundRef.current;
        if (current?.phase === 'actions' && current.seats[seatIndex].status === 'active') {
          setThinkingSeat(seatIndex);
        }
      });
      schedule(elapsed, () => {
        const current = roundRef.current;
        setThinkingSeat(null);
        if (!current || current.phase !== 'actions') return;
        const seat = current.seats[seatIndex];
        if (seat.status !== 'active' || seat.lastMove !== null || !seat.persona) return;
        const move = decideShowdownMove(
          seat,
          current.dealerCards,
          current.dealerCards.length,
          current.ante,
          seat.persona,
          rngRef.current,
        );
        setRound(applyMove(current, seatIndex, move));
        if (move.kind === 'bail') play('fold');
        else if (move.pressure) play('raise');
        else if (move.kind === 'forge') play('flip');
      });
    }
  }, [play, schedule, setRound]);

  /** Turns the next dealer card, once the table is ready for it. */
  const performDraw = useCallback(() => {
    const current = roundRef.current;
    if (!current) return;
    if (current.phase !== 'kicker' && current.phase !== 'actions') return;
    if (current.phase === 'kicker' && !allKickersSet(current)) return;
    if (current.phase === 'actions' && !allActed(current)) return;

    const next = dealDealerCard(current);
    setRound(next);
    play('flip');
    if (next.phase === 'actions') runBotTurns();
  }, [play, runBotTurns, setRound]);

  // Once every active seat has moved, the next card goes on the clock —
  // exactly once per action round, whichever seat happened to act last.
  useEffect(() => {
    if (!round || round.phase !== 'actions' || !allActed(round)) return;
    const nextCard = round.dealerCards.length + 1;
    if (drawScheduledForRef.current >= nextCard) return;
    drawScheduledForRef.current = nextCard;
    schedule(DEALER_CARD_BEAT_MS, performDraw);
  }, [round, performDraw, schedule]);

  const beginRound = useCallback(
    (table: ShowdownRoundState, ante?: Ante) => {
      clearTimers();
      settleScheduledRef.current = false;
      drawScheduledForRef.current = 0;
      setThinkingSeat(null);
      setKickerLocked(false);
      setForgeArming(false);
      setPressureArmed(false);
      setRevealed(false);
      setNewAchievements([]);
      rngRef.current = mulberry32(randomSeed());

      const dealing = startShowdownRound(table, rngRef.current, ante);
      setRound(dealing);
      play('chip');
      for (let beat = 0; beat < 5; beat += 1) {
        schedule(DEAL_BEAT_MS * (beat + 1), () => play('deal'));
      }
      schedule(DEAL_BEAT_MS * 5 + DEAL_SETTLE_MS, () => {
        const current = roundRef.current;
        if (!current || current.phase !== 'dealing') return;
        // Bots pick their kickers the moment the deal settles; the table
        // then waits on the human's pick before the climb starts.
        let next = finishDealing(current);
        for (let seatIndex = HUMAN_SEAT + 1; seatIndex < SEAT_COUNT; seatIndex += 1) {
          next = setKicker(next, seatIndex, chooseBotKicker(next.seats[seatIndex].cards));
        }
        setRound(next);
      });
    },
    [clearTimers, play, schedule, setRound],
  );

  /** Take a seat from the home screen. Callers gate on `canPlay` first. */
  const startHand = useCallback(
    (ante: Ante) => {
      if (!bankrollLoaded || !canPlay(bankroll, ante)) return;
      beginRound(createShowdownTable(bankroll, ante, 'You'));
    },
    [bankroll, bankrollLoaded, beginRound],
  );

  /** Marks (or re-marks) the human's kicker while the pick is still open. */
  const selectKicker = useCallback(
    (cardIndex: number) => {
      const current = roundRef.current;
      if (!current || current.phase !== 'kicker' || kickerLocked) return;
      setRound(setKicker(current, HUMAN_SEAT, cardIndex));
      play('tap');
    },
    [kickerLocked, play, setRound],
  );

  /** Locks the kicker in and puts the first dealer card on the clock. */
  const lockKicker = useCallback(() => {
    const current = roundRef.current;
    if (!current || current.phase !== 'kicker' || kickerLocked) return;
    if (humanSeat(current).kickerIndex === null) return;
    setKickerLocked(true);
    drawScheduledForRef.current = 1;
    play('chip');
    schedule(DEALER_CARD_BEAT_MS, performDraw);
  }, [kickerLocked, performDraw, play, schedule]);

  /** The human's move for this action round, with the pressure toggle folded in. */
  const submitMove = useCallback(
    (move: ShowdownMove) => {
      const current = roundRef.current;
      if (!current || current.phase !== 'actions') return;
      const seat = humanSeat(current);
      if (seat.status !== 'active' || seat.lastMove !== null) return;

      setRound(applyMove(current, HUMAN_SEAT, move));
      setForgeArming(false);
      setPressureArmed(false);
      if (move.kind === 'bail') play('fold');
      else if (move.pressure) play('raise');
      else if (move.kind === 'forge') play('flip');
      else play('tap');
    },
    [play, setRound],
  );

  const pressureNow = useCallback((): boolean => {
    const current = roundRef.current;
    if (!current || !pressureArmed) return false;
    const seat = humanSeat(current);
    return (
      current.dealerCards.length === PRESSURE_ROUND &&
      !seat.pressured &&
      seat.stack >= current.ante
    );
  }, [pressureArmed]);

  const hold = useCallback(() => {
    submitMove({ kind: 'hold', pressure: pressureNow() });
  }, [pressureNow, submitMove]);

  const forge = useCallback(
    (discardIndex: number) => {
      const current = roundRef.current;
      if (!current) return;
      const seat = humanSeat(current);
      const pressure = pressureNow();
      if (discardIndex === seat.kickerIndex) return;
      if (seat.forgesUsed >= 2 || seat.stack < current.ante * (pressure ? 2 : 1)) return;
      submitMove({ kind: 'forge', forgeDiscardIndex: discardIndex, pressure });
    },
    [pressureNow, submitMove],
  );

  const bail = useCallback(() => {
    submitMove({ kind: 'bail', pressure: false });
  }, [submitMove]);

  const beginForge = useCallback(() => setForgeArming(true), []);
  const cancelForge = useCallback(() => setForgeArming(false), []);

  // The fifth card flips the phase to 'reveal'; from there the hand plays
  // itself out on a timer — the table's hands turn, then the chips move.
  useEffect(() => {
    if (round?.phase !== 'reveal' || settleScheduledRef.current) return;
    settleScheduledRef.current = true;

    schedule(REVEAL_BEAT_MS, () => {
      play('reveal');
      setRevealed(true);
    });
    schedule(REVEAL_BEAT_MS + SETTLE_BEAT_MS, () => {
      const current = roundRef.current;
      if (!current || current.phase !== 'reveal') return;
      const settled = settleShowdown(current);
      setRound(settled);

      const seat = humanSeat(settled);
      const settlement = seat.settlement;
      const nextBankroll = seat.stack;
      saveBankroll(nextBankroll);
      if (settlement) {
        const entry = {
          category: evaluateHand(seat.cards).category,
          outcome: outcomeOf(settlement),
          kickerStrike: settlement.kickerStrike,
          pressured: seat.pressured,
          forges: seat.forgesUsed,
          net: settlement.net,
          at: Date.now(),
        };
        // Computed against the closed-over `stats` rather than the functional
        // updater form: the diff needs the "before" record to know which
        // badges are new, and nothing else writes stats between a hand
        // settling and this effect running.
        const nextStats = recordHandResult(stats, entry);
        const unlocked = newlyUnlockedAchievements(stats, nextStats);
        updateStats(() => nextStats);
        if (unlocked.length > 0) setNewAchievements(unlocked);

        if (settlement.sweep || settlement.kickerStrike) play('winBig');
        else if (settlement.net > 0) play('win');
        else if (settlement.net < 0) play('lose');
        else play('push');
      }
      if (isBust(nextBankroll, settled.ante)) {
        schedule(BUST_CUE_DELAY_MS, () => play('bust'));
      }
    });
  }, [round?.phase, play, saveBankroll, schedule, setRound, stats, updateStats]);

  const nextHand = useCallback(() => {
    const current = roundRef.current;
    if (!current || current.phase !== 'settled') return;
    if (!canPlay(humanSeat(current).stack, current.ante)) return;
    beginRound(current);
  }, [beginRound]);

  /**
   * The free top-up. `reUp` is a no-op unless the stack really is short, so
   * this stays correct even if a caller offers the button when it shouldn't.
   * `atAnte` is only consulted away from the table, where there is no round to
   * read the stake from.
   */
  const reUpNow = useCallback(
    (atAnte: Ante) => {
      const current = roundRef.current;
      const ante = current ? current.ante : atAnte;
      const before = current ? humanSeat(current).stack : bankroll;
      const topped = reUp(before, ante);
      if (topped === before) return;

      if (current) {
        setRound({
          ...current,
          seats: current.seats.map((seat, index) =>
            index === HUMAN_SEAT ? { ...seat, stack: topped } : seat,
          ),
        });
      }
      saveBankroll(topped);
      updateStats((prev) => ({ ...prev, reUps: prev.reUps + 1 }));
      play('chip');
    },
    [bankroll, play, saveBankroll, setRound, updateStats],
  );

  /**
   * The daily bonus drop. Eligibility lives in the daily-bonus store; this
   * just moves the chips, on or off the table.
   */
  const claimDailyBonus = useCallback(() => {
    const current = roundRef.current;
    const before = current ? humanSeat(current).stack : bankroll;
    const topped = before + DAILY_BONUS_AMOUNT;

    if (current) {
      setRound({
        ...current,
        seats: current.seats.map((seat, index) =>
          index === HUMAN_SEAT ? { ...seat, stack: topped } : seat,
        ),
      });
    }
    saveBankroll(topped);
    play('chip');
  }, [bankroll, play, saveBankroll, setRound]);

  /** Leaving mid-hand just abandons the presentation — nothing was settled. */
  const leaveTable = useCallback(() => {
    clearTimers();
    setThinkingSeat(null);
    setKickerLocked(false);
    setForgeArming(false);
    setPressureArmed(false);
    setRevealed(false);
    setNewAchievements([]);
    settleScheduledRef.current = false;
    drawScheduledForRef.current = 0;
    setRound(null);
  }, [clearTimers, setRound]);

  const dismissAchievements = useCallback(() => setNewAchievements([]), []);

  return {
    round,
    thinkingSeat,
    kickerLocked,
    forgeArming,
    pressureArmed,
    revealed,
    newAchievements,
    /** During a hand the seat's stack IS the bankroll — no second ledger. */
    bankroll: round ? humanSeat(round).stack : bankroll,
    bankrollLoaded,
    stats,
    startHand,
    selectKicker,
    lockKicker,
    hold,
    forge,
    bail,
    beginForge,
    cancelForge,
    setPressureArmed,
    nextHand,
    reUpNow,
    claimDailyBonus,
    dismissAchievements,
    leaveTable,
    resetBankroll,
  };
};

export type UseShowdownTableResult = ReturnType<typeof useShowdownTable>;
