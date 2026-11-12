# UI visual pack for Zah

Ako · 31 October 2026 · six sheets, everything the game draws or needs to

## What this is

Six SVG mock-ups covering every piece of UI in the game, built from what the code actually
renders today. Open them in any browser or drop them into Figma. Each one marks what already exists
in code versus what is new, so you know where you are restyling and where you are designing.

The headline, which surprised me: **almost all of the UI already exists.** Ticket cards, popups,
arrows that pin to the screen edge when the target is off-screen, bay signs, ground discs, a trick
callout, a gear readout, a results panel. Kyuri and I built them one system at a time, in code, with
default fonts and guessed positions. None of it has ever been laid out as one screen. That is the job:
not inventing the UI, making six independent things read as one game.

## The sheets

| sheet | what it shows | exists in code? |
| --- | --- | --- |
| `01-hud-layout.svg` | the whole screen, all six zones, in play | yes, all of it |
| `02-order-ticket-states.svg` | one ticket card through every state, plus the six popups | yes |
| `03-bay-and-world-markers.svg` | bay sign states, ground discs, the focused ring | yes |
| `04-trick-feedback-and-controls.svg` | trick callout, the eight trick names, the full controls screen | callout yes; controls screen is 6 of 11 |
| `05-tutorial-beats.svg` | the beat card, the done stamp, how the HUD reveals zone by zone | **new** |
| `06-pause-and-results.svg` | pause menu, end-of-shift ledger | pause broken; results is three plain lines |

## The palette is already decided

The code has a shared `OrderPalette` and everything on screen should draw from it. The rule it
encodes is the one thing a player needs to learn:

| colour | hex | means |
| --- | --- | --- |
| Pickup blue | `#3399FF` | go and fetch something |
| Cook orange | `#FA8C1E` | something is cooking, be elsewhere |
| Deliver green | `#33D959` | money is coming |
| Warning yellow | `#FFD91A` | hurry |
| Urgent red | `#F2261F` | money is leaving |
| Paper | `#FAEDC7` | card background |
| Ink | `#40261A` | card text |
| Panel dark | `#1F140F` at 78% | HUD panel background |
| Money gold | `#FFCC33` | the number that matters |

Green means money arriving, red means money leaving, orange means you should be driving away. A
player who learns five colours has learned the whole order system. Please keep it to these.

## What is yours to decide

- **Type.** Everything is Unity's default font at guessed sizes. Pick one display face for the
  trick callouts and the clock, one text face for everything else. The trick names are fixed strings
  up to 16 characters (`CRAZY BACK DRIFT`) and need to fit at 56 px.
- **Layout.** Sheet 01 is my proposed arrangement, not a spec. The constraints are: clock top-right
  because it is glanced at most; tickets top-left because they are read in sequence; trick callout
  bottom-left near the car; nothing in the centre third of the screen except arrows, because that is
  where the road is.
- **Motion.** Popups hold 2.5 s. Trick callouts hold 1 s. Beat cards slide. Urgent cards pulse.
  What those look like is yours.
- **The beat card and done stamp.** The only genuinely new components. One card design, one
  stamp. Sheet 05.
- **The pause menu and results ledger.** Both exist as code with no design. Sheet 06.

## What is not yours

- The strings. Every piece of text in these mocks is a real string from the code or the tuning
  asset. Changing wording means changing code, so flag it rather than redrawing it.
- The disc radius. The ground disc is drawn at the real 6 m trigger radius. Making it look bigger or
  smaller teaches the player the wrong hitbox.
- The star thresholds and money values. Those are Kyuri's numbers and are not final.

## Two bugs you should know about before you start

1. **Pause renders underneath the HUD.** The arrows and the trick text draw over the pause panel,
   and the trick readout sits on top of the pause button. Whatever you build for pause needs to be on
   a canvas with a higher sorting order than everything else (the trick HUD is at 9; go to 20 or
   above).
2. **The in-game controls screen lists six of eleven actions.** Sheet 04 has the full set in the
   grouping I would suggest: Drive, Tricks the easy way, Tricks the arcade way. The two-column trick
   split is deliberate: the game has a one-button trick layer for people who want it simple and the
   original gear rhythm for people who want it authentic, and the screen should present them as two
   routes to the same result rather than one list of eleven keys.

## Order of work, if it helps

1. Sheet 06 pause — it is blocking playtesting, because testers cannot stop to read anything.
2. Sheet 04 controls screen — one screen, all the content is in the sheet.
3. Sheet 01 layout pass — restyle what exists so the six zones agree with each other.
4. Sheet 05 beat card and stamp — needed for the tutorial, which is the three-day plan.
5. Sheet 02 and 03 polish — the states already work; this is making them beautiful.

Shout if any of this is wrong about what the code does. I read it today but I did not run it.
