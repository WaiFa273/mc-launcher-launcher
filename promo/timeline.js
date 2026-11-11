// timeline.js — the single source of truth for time. Imported by the page (picture) AND score.mjs (sound),
// so every cut, hit and sound effect lands on the same number. Plain ES module: no DOM, no Node APIs.

export const BPM = 96;
export const BEAT = 60 / BPM;          // 0.625 s
export const BAR = BEAT * 4;           // 2.5 s
/** beat number → seconds (fractions allowed: b(4.5) is the "and" of beat 5) */
export const b = n => n * BEAT;
/** When to START a transition so the new picture first shows ON the hit (a beat / bar time). */
export const revealOn = (hit, dur, reveal = 0) => hit - reveal * dur;

export const DUR = b(64);              // 40.0 s — 16 bars at 96 BPM

// Shots / pages on the grid. Eight pages, all on bar lines.
export const SHOTS = [
  { id: 'hook',   t0: b(0),  t1: b(8)  },   // 0.0  – 5.0    six paths fall in, collapse to a dot
  { id: 'reveal', t0: b(8),  t1: b(16) },   // 5.0  – 10.0   the dot becomes a rule; title +主界面
  { id: 'scan',   t0: b(16), t1: b(24) },   // 10.0 – 15.0   01 收录
  { id: 'launch', t0: b(24), t1: b(36) },   // 15.0 – 22.5   02 启动（六次点击，3/6 → 6/6）
  { id: 'chain',  t0: b(36), t1: b(44) },   // 22.5 – 27.5   03 接力
  { id: 'report', t0: b(44), t1: b(52) },   // 27.5 – 32.5   04 体检
  { id: 'spec',   t0: b(52), t1: b(60) },   // 32.5 – 37.5   规格：259 KB
  { id: 'end',    t0: b(60), t1: DUR   },   // 37.5 – 40.0   落版
];

// Picture events that need a sound.
export const CUES = [
  // six paths land
  ...[0, 1, 2, 3, 4, 5].map(i => ({ t: b(0.5) + i * b(0.5), sfx: 'tick' })),
  { t: b(6), sfx: 'whoosh' },              // collapse
  { t: b(7), sfx: 'pop' },                 // the dot
  { t: b(8), sfx: 'boom' },                // reveal
  { t: b(16), sfx: 'swipe' },              // page 01
  { t: b(24), sfx: 'swipe' },              // page 02
  ...[0, 1, 2, 3, 4, 5].map(i => ({ t: b(25) + i * b(1), sfx: 'click' })),  // six launches
  { t: b(36), sfx: 'swipe' },              // page 03
  { t: b(37), sfx: 'ping' },
  { t: b(38.5), sfx: 'ping' },
  { t: b(40), sfx: 'ping' },
  { t: b(44), sfx: 'swipe' },              // page 04
  { t: b(46), sfx: 'ping' },               // the score lands on 89
  { t: b(52), sfx: 'swipe' },              // spec
  { t: b(53), sfx: 'tick' },
  { t: b(54), sfx: 'tick' },
  { t: b(55), sfx: 'tick' },
  { t: b(60), sfx: 'whoosh' },             // to the end card
  { t: b(61), sfx: 'pop' },                // logo
];

// Deliberate still moments (reported as intended holds, not dead air).
export const HOLDS = [[b(62), DUR]];
// The big hits that should follow a breath.
export const DROPS = [b(8)];
