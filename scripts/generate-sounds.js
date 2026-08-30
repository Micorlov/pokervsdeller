/* eslint-disable no-console */
/**
 * Synthesises the app's sound effects into `assets/sounds/*.wav`.
 *
 * The cues are generated rather than sampled so the whole set stays in one
 * place, tunes by editing numbers, and carries no third-party licence. The
 * palette matches the lamp-lit felt: filtered hiss for cards, a bright clay
 * click for chips, warm struck-bar chimes for money, nothing shrill.
 *
 * Run with: node scripts/generate-sounds.js
 */

const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const SAMPLE_RATE = 44100;
const OUT_DIR = path.join(__dirname, '..', 'assets', 'sounds');

/**
 * The long celebratory cues ship as AAC — they are ~8x smaller and their
 * encoder priming delay is inaudible on a sound that follows a screen change.
 * The short interaction cues stay uncompressed: a tick that arrives 50ms after
 * the finger no longer belongs to the tap.
 */
const COMPRESSED = new Set(['win', 'winBig', 'lose', 'push', 'bust']);

// ---------------------------------------------------------------------------
// Buffer helpers — every generator returns a Float32Array of mono samples.
// ---------------------------------------------------------------------------

const seconds = (n) => Math.round(n * SAMPLE_RATE);

const silence = (duration) => new Float32Array(seconds(duration));

/** Exponential decay, the natural shape for anything struck or plucked. */
const decay = (t, tau) => Math.exp(-t / tau);

/** Short raised-cosine fade so a burst never starts on a click. */
const attack = (t, rise) => (t >= rise ? 1 : 0.5 - 0.5 * Math.cos((Math.PI * t) / rise));

const mixInto = (target, source, atSeconds = 0) => {
  const offset = seconds(atSeconds);
  for (let i = 0; i < source.length; i += 1) {
    const index = offset + i;
    if (index < target.length) target[index] += source[i];
  }
  return target;
};

/** A decaying sine partial — the building block of every tuned sound. */
const partial = (duration, freq, { amp = 1, tau = 0.2, rise = 0.002, phase = 0 } = {}) => {
  const out = silence(duration);
  for (let i = 0; i < out.length; i += 1) {
    const t = i / SAMPLE_RATE;
    out[i] = amp * attack(t, rise) * decay(t, tau) * Math.sin(2 * Math.PI * freq * t + phase);
  }
  return out;
};

/** Deterministic noise — a fixed seed keeps regenerated files byte-identical. */
const makeRandom = (seed) => {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return (state / 0xffffffff) * 2 - 1;
  };
};

/** Band-passed noise burst: the paper hiss of a card sliding off the deck. */
const noiseBurst = (duration, { amp = 1, tau = 0.03, seed = 1, lowpass = 0.5, highpass = 0 } = {}) => {
  const out = silence(duration);
  const random = makeRandom(seed);
  let lowState = 0;
  let dcState = 0;
  for (let i = 0; i < out.length; i += 1) {
    const t = i / SAMPLE_RATE;
    lowState += lowpass * (random() - lowState);
    let sample = lowState;
    if (highpass > 0) {
      dcState += highpass * (sample - dcState);
      sample -= dcState;
    }
    out[i] = amp * decay(t, tau) * sample;
  }
  return out;
};

/** One struck body: a click, its resonant modes, and a little thump — the dealer's knock. */
const woodKnock = (duration, { pitch = 1, amp = 1, seed = 7 } = {}) => {
  const out = silence(duration);
  mixInto(out, noiseBurst(duration, { amp: 0.5 * amp, tau: 0.006, seed, lowpass: 0.55, highpass: 0.02 }));
  mixInto(out, partial(duration, 190 * pitch, { amp: 0.5 * amp, tau: 0.05, rise: 0.0008 }));
  mixInto(out, partial(duration, 780 * pitch, { amp: 0.28 * amp, tau: 0.035, rise: 0.0006 }));
  mixInto(out, partial(duration, 1830 * pitch, { amp: 0.16 * amp, tau: 0.018, rise: 0.0004 }));
  mixInto(out, partial(duration, 3260 * pitch, { amp: 0.07 * amp, tau: 0.01, rise: 0.0003 }));
  return out;
};

/** A struck metal bar — inharmonic partials, long tail, no attack transient. */
const chime = (duration, freq, { amp = 1, tau = 0.5, at = 0 } = {}) => {
  const out = silence(duration);
  const modes = [
    [1, 1, 1],
    [2.01, 0.42, 0.7],
    [3.02, 0.2, 0.5],
    [4.18, 0.11, 0.34],
    [5.44, 0.05, 0.22],
  ];
  modes.forEach(([ratio, level, tauScale]) => {
    mixInto(
      out,
      partial(Math.max(0.02, duration - at), freq * ratio, {
        amp: amp * level,
        tau: tau * tauScale,
        rise: 0.004,
      }),
      at,
    );
  });
  return out;
};

// ---------------------------------------------------------------------------
// Output — normalise, soft-clip, then write a 16-bit mono WAV.
// ---------------------------------------------------------------------------

const normalise = (samples, peak = 0.82) => {
  let max = 0;
  for (let i = 0; i < samples.length; i += 1) max = Math.max(max, Math.abs(samples[i]));
  if (max === 0) return samples;
  const gain = peak / max;
  for (let i = 0; i < samples.length; i += 1) samples[i] = Math.tanh(samples[i] * gain);
  return samples;
};

/** Fade the last few ms to zero so playback never ends on a discontinuity. */
const fadeOut = (samples, tail = 0.01) => {
  const length = Math.min(samples.length, seconds(tail));
  for (let i = 0; i < length; i += 1) {
    samples[samples.length - length + i] *= 1 - i / length;
  }
  return samples;
};

const writeWav = (name, samples) => {
  const finished = fadeOut(normalise(samples));
  const dataLength = finished.length * 2;
  const buffer = Buffer.alloc(44 + dataLength);

  buffer.write('RIFF', 0);
  buffer.writeUInt32LE(36 + dataLength, 4);
  buffer.write('WAVE', 8);
  buffer.write('fmt ', 12);
  buffer.writeUInt32LE(16, 16); // PCM chunk size
  buffer.writeUInt16LE(1, 20); // format: PCM
  buffer.writeUInt16LE(1, 22); // channels: mono
  buffer.writeUInt32LE(SAMPLE_RATE, 24);
  buffer.writeUInt32LE(SAMPLE_RATE * 2, 28); // byte rate
  buffer.writeUInt16LE(2, 32); // block align
  buffer.writeUInt16LE(16, 34); // bits per sample
  buffer.write('data', 36);
  buffer.writeUInt32LE(dataLength, 40);

  for (let i = 0; i < finished.length; i += 1) {
    const clamped = Math.max(-1, Math.min(1, finished[i]));
    buffer.writeInt16LE(Math.round(clamped * 32767), 44 + i * 2);
  }

  const wavPath = path.join(OUT_DIR, `${name}.wav`);
  fs.writeFileSync(wavPath, buffer);

  if (!COMPRESSED.has(name)) {
    report(`${name}.wav`, buffer.length, finished.length);
    return;
  }

  const m4aPath = path.join(OUT_DIR, `${name}.m4a`);
  execFileSync('afconvert', ['-f', 'm4af', '-d', 'aac', '-b', '96000', wavPath, m4aPath]);
  fs.unlinkSync(wavPath);
  report(`${name}.m4a`, fs.statSync(m4aPath).size, finished.length);
};

const report = (file, bytes, sampleCount) => {
  console.log(`${file.padEnd(20)}${(bytes / 1024).toFixed(1).padStart(7)} KB   ${(sampleCount / SAMPLE_RATE).toFixed(2)}s`);
};

// ---------------------------------------------------------------------------
// The cues.
// ---------------------------------------------------------------------------

// A pentatonic set in C, so any two cues heard together still agree.
const NOTE = {
  C4: 261.63,
  E4: 329.63,
  A4: 440.0,
  G4: 392.0,
  C5: 523.25,
  D5: 587.33,
  E5: 659.25,
  G5: 783.99,
  C6: 1046.5,
};

const cues = {
  /** A card sliding off the deck: a short filtered hiss, no pitch at all. */
  deal: () => {
    const out = silence(0.16);
    mixInto(out, noiseBurst(0.16, { amp: 0.55, tau: 0.035, seed: 61, lowpass: 0.42, highpass: 0.06 }));
    mixInto(out, partial(0.16, 620, { amp: 0.1, tau: 0.02, rise: 0.001 }));
    return out;
  },

  /** A card turning over — the same hiss, snapped shorter and pitched up. */
  flip: () => {
    const out = silence(0.12);
    mixInto(out, noiseBurst(0.12, { amp: 0.45, tau: 0.012, seed: 27, lowpass: 0.7, highpass: 0.12 }));
    mixInto(out, partial(0.12, 1900, { amp: 0.26, tau: 0.014, rise: 0.0005 }));
    return out;
  },

  /** One clay chip landing on felt: a tick with a short bright ring over it. */
  chip: () => {
    const out = silence(0.3);
    mixInto(out, noiseBurst(0.3, { amp: 0.3, tau: 0.005, seed: 13, lowpass: 0.75, highpass: 0.2 }));
    mixInto(out, chime(0.3, NOTE.C6, { amp: 0.34, tau: 0.09 }));
    mixInto(out, partial(0.3, 240, { amp: 0.22, tau: 0.03, rise: 0.001 }));
    return out;
  },

  /** The player's raise: two chips, so committing sounds bigger than anteing. */
  raise: () => {
    const out = silence(0.42);
    [0, 0.085].forEach((at, index) => {
      mixInto(out, noiseBurst(0.2, { amp: 0.3, tau: 0.005, seed: 13 + index * 40, lowpass: 0.75, highpass: 0.2 }), at);
      mixInto(out, chime(0.42 - at, NOTE.C6, { amp: 0.34 - index * 0.04, tau: 0.1 }), at);
      mixInto(out, partial(0.2, 240, { amp: 0.22, tau: 0.03, rise: 0.001 }), at);
    });
    return out;
  },

  /** Cards pushed away: a knuckle on the table, low and unresonant. */
  fold: () => {
    const out = silence(0.3);
    mixInto(out, noiseBurst(0.3, { amp: 0.32, tau: 0.008, seed: 8, lowpass: 0.25 }));
    mixInto(out, partial(0.3, 128, { amp: 0.7, tau: 0.06, rise: 0.001 }));
    mixInto(out, partial(0.3, 260, { amp: 0.2, tau: 0.03, rise: 0.001 }));
    return out;
  },

  /** The dealer turning the hole cards — the firmest knock in the set. */
  reveal: () => woodKnock(0.35, { pitch: 1, amp: 1, seed: 33 }),

  /** Winning the hand: a rising triad that resolves upward. */
  win: () => {
    const out = silence(1.5);
    [NOTE.C5, NOTE.E5, NOTE.G5].forEach((freq, index) => {
      mixInto(out, chime(1.5, freq, { amp: 0.5, tau: 0.6, at: index * 0.1 }));
    });
    return out;
  },

  /** A flush or better: the full arpeggio, with a chip hitting the felt under it. */
  winBig: () => {
    const out = silence(2.4);
    [
      [NOTE.G4, 0.0],
      [NOTE.C5, 0.075],
      [NOTE.E5, 0.15],
      [NOTE.G5, 0.225],
      [NOTE.C6, 0.32],
      [NOTE.G5, 0.5],
    ].forEach(([freq, at], index) => {
      mixInto(out, chime(2.4, freq, { amp: 0.44 - index * 0.015, tau: 0.72, at }));
    });
    mixInto(out, woodKnock(2.4, { pitch: 1.05, amp: 0.45, seed: 3 }));
    return out;
  },

  /** Losing: the same interval, falling, and left unresolved. */
  lose: () => {
    const out = silence(1.2);
    mixInto(out, chime(1.2, NOTE.E4, { amp: 0.45, tau: 0.45 }));
    mixInto(out, chime(1.2, NOTE.C4, { amp: 0.4, tau: 0.6, at: 0.13 }));
    return out;
  },

  /** A push, or a dealer who did not qualify: one soft note. Nothing happened. */
  push: () => {
    const out = silence(0.8);
    mixInto(out, chime(0.8, NOTE.A4, { amp: 0.4, tau: 0.42 }));
    return out;
  },

  /** The stack is gone: low, slow, and deliberately short of a cadence. */
  bust: () => {
    const out = silence(2.0);
    mixInto(out, chime(2.0, NOTE.A4 / 2, { amp: 0.45, tau: 0.7 }));
    mixInto(out, chime(2.0, NOTE.E4 / 2, { amp: 0.4, tau: 0.8, at: 0.18 }));
    mixInto(out, chime(2.0, NOTE.C4 / 2, { amp: 0.35, tau: 0.95, at: 0.36 }));
    return out;
  },

  /** Menus and chrome. Deliberately the quietest cue in the set. */
  tap: () => {
    const out = silence(0.07);
    mixInto(out, noiseBurst(0.07, { amp: 0.22, tau: 0.004, seed: 44, lowpass: 0.6, highpass: 0.1 }));
    mixInto(out, partial(0.07, 1500, { amp: 0.22, tau: 0.011, rise: 0.0006 }));
    return out;
  },
};

fs.mkdirSync(OUT_DIR, { recursive: true });
Object.entries(cues).forEach(([name, build]) => writeWav(name, build()));
console.log(`\nWrote ${Object.keys(cues).length} cues to ${path.relative(process.cwd(), OUT_DIR)}`);
