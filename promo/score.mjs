// score.mjs — 启动器启动器 · 40s promo
// quiet, competent, slightly deadpan: a pluck figure carries it, pad sits behind, the kick only ever plays one and three.
// Build: node <skill>/scripts/audio.mjs build promo/score.mjs --out audio/music.wav
import { DUR, BPM, CUES, b } from './timeline.js';
export { DUR };

export default function score(S) {
  S.tempo(BPM);
  S.space({ room: 0.22, damp: 0.5, width: 0.7 });

  const drums = S.bus('drums', { gain: 0.5 });
  const bass = S.bus('bass', { gain: 0.46, duck: 0.4 });
  const pad = S.bus('pad', { gain: 0.26, reverb: 0.42, duck: 0.55, highpass: 130 });
  const pluck = S.bus('pluck', { gain: 0.42, reverb: 0.3, delay: 0.16 });
  const sfx = S.bus('sfx', { gain: 0.6, reverb: 0.12 });

  const bar = S.bar(1);              // 2.5 s
  const beat = S.beat(1);            // 0.625 s
  const prog = [['C3', 'maj7'], ['A2', 'min7'], ['F2', 'maj7'], ['G2', 'sus4']];
  const totalBars = Math.round(DUR / bar);   // 16

  // an eight-note figure that walks the chord, accent on the first beat
  const SHAPE = [0, 2, 3, 4, 3, 2, 4, 3];
  const pluckBar = (t, chordMidi, q, { density = 1, gain = 0.44, until = Infinity } = {}) => {
    const notes = S.chord(chordMidi + 12, q);
    for (let i = 0; i < 8; i++) {
      if (density < 1 && i % 2 === 1) continue;
      const at = t + i * S.beat(0.5);
      if (at >= until) continue;
      const idx = SHAPE[i] % notes.length;
      const note = notes[idx] + (SHAPE[i] >= notes.length ? 12 : 0);
      const accent = i === 0 ? 1 : i === 4 ? 0.7 : 0.52;
      pluck.add(S.pluck(note, 1.4), at, { gain: gain * accent, pan: i % 2 ? 0.22 : -0.14 });
    }
  };

  for (let bi = 0; bi < totalBars; bi++) {
    const t = S.bar(bi);
    const [root, q] = prog[Math.floor(bi / 2) % prog.length];
    const chordMidi = S.midi(root);

    const intro = bi < 2;          // 0–5 s    hook: the figure alone
    const build = bi < 4;          // 5–10 s   reveal: pad, pad, heartbeat
    const body = bi < 12;          // 10–30 s  launch / chain / report
    const calm = bi < 14;          // 30–35 s  spec: drums out
    const out = bi >= 14;          // 35–40 s  end card

    if (out) {
      if (bi === 14) {
        pad.add(S.pad(S.chord(chordMidi + 12, 'maj7'), bar * 1.9, { cutoff: 1400 }), t, { gain: 0.55 });
        pluck.add(S.pluck(chordMidi + 24, 2.2), t, { gain: 0.44 });
        pluck.add(S.pluck(chordMidi + 31, 2.0), t + beat * 0.5, { gain: 0.26, pan: 0.3 });
        bass.add(S.bass(root, bar * 1.5, { cutoff: 380, sub: 0.25 }), t, { gain: 0.5 });
      }
      if (bi === 15) pad.add(S.pad(S.chord(chordMidi + 12, 'maj7'), bar * 1.5, { cutoff: 900 }), t, { gain: 0.32 });
      continue;
    }

    // pad: one chord per two bars
    if (bi % 2 === 0) {
      const cutoff = intro ? 700 : build ? 1500 : calm ? 1100 : 2000;
      pad.add(S.pad(S.chord(chordMidi + 12, q), intro ? bar * 1.5 : bar * 1.95, { cutoff, release: intro ? 0.4 : 1.2 }), t, { gain: intro ? 0.65 : 1 });
    }

    // leave a breath before the reveal hit at 5.0s
    pluckBar(t, chordMidi, q, { density: intro ? 0.5 : calm ? 0.75 : 1, gain: intro ? 0.34 : 0.44, until: intro ? (t < 3 ? Infinity : 4.35) : Infinity });

    if (!intro) {
      // a soft heartbeat on one and three — no four-on-the-floor in this film
      for (const k of [0, 2]) {
        drums.add(S.kick({ decay: 0.26 }), t + k * beat, { gain: 0.6 });
        S.duck(t + k * beat, 0.4);
      }
      bass.add(S.bass(root, beat * 0.8, { cutoff: 420, sub: 0.28 }), t, { gain: 0.58 });
      bass.add(S.bass(root, beat * 0.6, { cutoff: 420, sub: 0.22 }), t + beat * 2, { gain: 0.4 });
    }

    if (body || calm) {
      for (let i = 0; i < 8; i++) {
        drums.add(S.shaker({ decay: 0.03 }), t + i * S.beat(0.5), { gain: i % 2 ? 0.14 : 0.24, pan: 0.28 });
      }
    }
    if (body && bi % 2 === 1) drums.add(S.rim({}), t + beat * 3.5, { gain: 0.18, pan: -0.3 });
  }

  // ── sound design, driven by the same cue list the picture uses ──
  const at = name => CUES.filter(c => c.sfx === name).map(c => c.t);
  at('tick').forEach((t, i) => sfx.add(S.tick(), t, { gain: 0.2, pan: -0.35 + (i % 6) * 0.13 }));
  at('click').forEach((t, i) => sfx.add(S.click(), t, { gain: 0.28, pan: -0.2 + i * 0.07 }));
  at('pop').forEach(t => sfx.add(S.pop('C6'), t, { gain: 0.36 }));
  at('swipe').forEach(t => sfx.add(S.swipe(0.26), t - 0.05, { gain: 0.3, pan: -0.1 }));
  at('ping').forEach((t, i) => sfx.add(S.bell('E6', 1.6), t, { gain: 0.2 - i * 0.03, pan: 0.2 }));
  at('whoosh').forEach(t => sfx.add(S.whoosh(0.7), t - 0.45, { gain: 0.4 }));
  at('boom').forEach(t => { sfx.add(S.boom(), t, { gain: 0.5 }); S.duck(t, 0.85); });

  // a small lift into the end card, then let it ring
  sfx.add(S.riser(bar * 0.8), b(58), { gain: 0.26 });
  sfx.add(S.shimmer(2.2), DUR - 2.6, { gain: 0.2 });
}
