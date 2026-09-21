# Where we get assets for free, and what the licences actually say

I went looking for free assets we can legally use, because our pitch deck had no credits slide and
that needs fixing. This is the list plus what each licence actually requires from us.

We are greyboxing first, so we do not need much yet. But when we do grab something, the rule is:
**download it and add a row to `docs/CREDITS.md` the same day.** Trying to remember where 30 files
came from in November is how people lose marks.

## The licence types in plain terms

| Licence | What it means for us |
|---|---|
| CC0 / public domain | Use it, change it, no credit needed. Easiest option. We still credit, because it is polite and it looks better in a submission. |
| CC-BY | Free to use, but we **must** name the author. If we forget, we are using it without permission. |
| CC-BY-SA | Credit required, and derivative work has to use the same licence. Avoid for coursework, it gets complicated. |
| OFL (fonts) | Free to use and embed. Cannot be sold on its own. Fine for us. |
| MIT / Apache (code) | Free to use, keep the licence text with the code. Fine, just keep the file. |
| Unity Asset Store standard EULA | See the warning below. Not the same as CC0. |

## 3D models

| Source | Licence | Credit needed? | Why it suits us |
|---|---|---|---|
| [Kenney](https://kenney.nl/assets/category:3D) | CC0 | Not required, we will anyway | [Car Kit](https://kenney.nl/assets/car-kit), [City Kit (Roads)](https://kenney.nl/assets/city-kit-roads), [City Kit (Suburban)](https://kenney.nl/assets/city-kit-suburban), [Retro Urban Kit](https://kenney.nl/assets/retro-urban-kit), [Racing Pack](https://kenney.nl/assets/racing-pack). Chunky low-poly style that matches a playful delivery game, and it is all one consistent art style so it does not look stitched together. |
| [Quaternius](https://quaternius.com/) | CC0 ([stated in their FAQ](https://quaternius.com/faq.html)) | Not required | Has a [LowPoly Cars pack](https://quaternius.itch.io/lowpoly-cars). Good backup if we want a different car shape. |
| [Poly Pizza](https://poly.pizza/) | **Mixed** — some CC0, some CC-BY | Depends per model | Big library, but the licence is per model, so I have to check each download individually. Not one blanket licence. |
| [OpenGameArt](https://opengameart.org/) | **Mixed** per submission | Depends per file | Kenney reposts a lot of packs here too. Check the licence field on the page every time. |

Kenney is our default. One style, one licence, no thinking required.

## Audio

| Source | Licence | Credit needed? | Notes |
|---|---|---|---|
| [Kenney UI Audio](https://kenney.nl/assets/ui-audio) and [Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 | No | Order arriving, delivery confirmed, pickup rejected. Exactly the feedback Kyuri's system needs. |
| [Freesound](https://freesound.org/) | **Mixed** — CC0 or CC-BY per sound | Depends per sound | Good for engine and crash sounds. The CC-BY ones need the uploader's name in credits. Check before downloading, not after. |
| [Pixabay sound effects](https://pixabay.com/sound-effects/) | Pixabay Content License | No | Easy option for car engine and impact sounds. Not CC0 though — see the note below. |

### Car audio, which the list above was missing

The priority order below used to jump from "nothing yet" straight to Kyuri's order-feedback sounds,
which skipped the car entirely. That is the wrong gap to leave open. There is an `AudioListener` on
the chase camera and **not one `AudioSource` anywhere in the project**, so the car is currently
silent at 25 m/s. A cube with good engine audio feels heavier than a nice model in silence, so this
comes before the Kenney Car Kit, not after it.

Four sounds, and only four:

| Sound | Driven by | Where to get it |
|---|---|---|
| Engine loop, pitch-shifted by speed | `VehicleController.ForwardSpeed` against `tuning.topSpeed` | [Freesound](https://freesound.org/search/), filtered to CC0 — search "engine loop" or "engine idle" |
| Tyre skid, faded in on slide | `VehicleController.LateralSpeed` — already public | [Freesound](https://freesound.org/search/) CC0, search "tyre skid" or "tire squeal" |
| Impact thud on collision | `OnCollisionEnter` relative velocity | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) — CC0, no licence checking needed |
| Landing thump after a jump | `WheelsOnGround` going 0 to non-zero | Same Kenney Impact Sounds pack |

Kenney covers two of the four outright. For the engine and skid, use Freesound with the licence
filter set to CC0 **before** downloading, not after — the site is mixed CC0 and CC-BY and the filter
is the whole difference between one credits row and a licence problem.

A note on why these four and not a full engine-audio system. Every one of them reads a value the
vehicle code already exposes publicly — `ForwardSpeed`, `LateralSpeed`, `WheelsOnGround`. No changes
to `VehicleController` are needed to wire any of it up, which means audio can land without touching
tuned physics.

Avoid Pixabay for anything committed to the repo, for the reason directly below.

One correction on Pixabay. It is free for commercial use with no attribution required, but the
content is royalty-free rather than public domain, and it stays copyrighted. Pixabay's own
[intellectual property explainer](https://pixabay.com/blog/posts/intellectual-property-explained-441/)
states that redistributing their content as standalone products is not allowed. Dropping a `.wav`
into a build is clearly fine. Committing the raw file to a repo strangers can browse sits closer to
the line than a CC0 file would. One more reason the repo stays private, and one more reason Kenney is
the safer default for anything we commit.
*Content was rephrased for compliance with licensing restrictions.*

## UI and fonts

| Source | Licence | Credit needed? | Notes |
|---|---|---|---|
| [Kenney UI Pack](https://opengameart.org/content/ui-pack) and [Game Icons](https://opengameart.org/content/game-icons) | CC0 | No | Timer bars, panels, arrows. Zubuhle's area but worth listing. |
| [Google Fonts](https://fonts.google.com/) | OFL or Apache ([their FAQ](https://developers.google.com/webfonts/faq)) | No | Free for commercial use and embedding. A heavy condensed font would suit the arcade look. |

## Code we can look at

We are writing our own controller, but these are worth reading before I start guessing at physics.
Each one has its own licence file, so I check it before copying anything in.

Licences below were read from each repo's GitHub metadata on 7 September 2026.

| Repo | Licence | Can we copy code? | What it is |
|---|---|---|---|
| [SergeyMakeev/ArcadeCarPhysics](https://github.com/SergeyMakeev/ArcadeCarPhysics) | MIT | Yes, keep the licence text | Arcade vehicle physics aimed at exactly our reference point, mentions Rocket League style handling |
| [MurielM87/Arcade-Vehicle-Controller](https://github.com/MurielM87/Arcade-Vehicle-Controller) | MIT | Yes, keep the licence text | Arcade vehicle controller in Unity |
| [Delt06/arcade-car-controller](https://github.com/Delt06/arcade-car-controller) | **None declared** | **No** | Scripts for arcade car physics. No licence file means default copyright applies and no reuse permission is granted. Read it for ideas only, copy nothing. |
| [benmcinnes/ArcadeVehiclePhysics](https://github.com/benmcinnes/ArcadeVehiclePhysics) | MIT | Yes, keep the licence text | Framework for arcade vehicle physics in Unity |

The `Delt06` repo is the one to be careful with. "Public on GitHub" is not the same as "licensed for
reuse" — with no licence file, the default position is that all rights are reserved and we have no
permission to copy any of it. Reading it to understand an approach is fine. Lifting a method is not.

If I use a chunk of someone's code, it goes in `_Project/ThirdParty` with its licence file and a row
in credits. If I only read it and wrote my own version, I still note it as a reference. That is
cheaper than being asked where something came from and not knowing.

## Warning about the Unity Asset Store

This one caught me out, so writing it down. "Free" on the Asset Store does not mean CC0. Under the
[Asset Store Terms of Service and EULA](https://unity.com/legal/as-terms) the standard licence does
not let you copy, distribute or prepare derivative works from an asset unless it says otherwise. You
can ship it inside a built game, but committing the raw source files to a repo other people can read
is a different thing, and it is only allowed if that specific publisher chose a looser licence.

What that means for us in practice:

- Our repo stays **private**. That is the main reason.
- If we do use an Asset Store package, it goes in `_Project/ThirdParty` and gets noted in credits
  with the publisher name.
- If we ever have to submit or share the project publicly, I check those packages first.
- Free CC0 sites like Kenney and Quaternius do not have this problem at all, which is another reason
  to default to them.

## What we actually need, in order

Not downloading a library of stuff we never use. This is the order things are genuinely needed:

1. **Nothing, for now.** The greybox route is Unity primitives, and my test scene is a plane and a
   wall. Cubes are fine and they keep the build small. Verified 7 September 2026: the car needs zero
   external assets to drive and to measure. Suspension is four raycasts, so there are no wheel meshes
   to model, the body is two primitive cubes, and the lab floor, markers, wall and cones are all
   generated in code with materials made at runtime.
2. **Car audio.** Engine loop, tyre skid, impact, landing. Four files, all driven off values the
   controller already exposes. This moved ahead of the car model because the car is currently silent
   and silence is what makes good physics feel weightless.
3. **A car model** once the handling feels right. Kenney Car Kit. Only after tuning, because a nice
   model on a bad car is a waste of time. It replaces the `Body Visual` cube's mesh only — the
   `BoxCollider` stays exactly as it is, so dropping the model in cannot change the handling we tuned.
4. **Feedback sounds** when Kyuri's orders work — order arrives, delivered, pickup refused. Kenney
   UI Audio.
5. **A font and UI bits** when Zubuhle builds the timer and order display.
6. **Buildings** for the art pass, late, and only from one Kenney city kit so it stays consistent.
7. **Skid marks and dust**, last and optional. `LateralSpeed` is already public, which is the only
   signal a skid-mark trail needs, so this is cheap whenever we want it.

## Credits table we fill in as we go

This is the format in `docs/CREDITS.md`:

| Asset | Author | Source URL | Licence | Where we use it |
|---|---|---|---|---|
| Car Kit | Kenney | https://kenney.nl/assets/car-kit | CC0 | Player vehicle |
| | | | | |

## Referencing, for the deck and the write-up

The credits table above is the *asset record*. It is not a reference list, and a marker looking for
referencing will want the second thing as well. Two different jobs:

- **`docs/CREDITS.md`** — the working log. Every downloaded file, its author, source, licence and
  where it is used. Filled in the day we download.
- **Reference list** — the formal citations in the deck and any written submission. Games we drew
  design from, assets we used, and code we learned from.

Harvard style, which is what most of our other modules want. Swap to APA if the brief says so, the
information needed is the same.

**Design references, the two games:**

> Psyonix. (2015) *Rocket League*. [Video game]. San Diego, CA: Psyonix.
>
> Ghost Town Games. (2016) *Overcooked*. [Video game]. Wakefield: Team17.

Cited for momentum, chase camera and recovery, and for automatic orders, timers and prioritisation
respectively.

**Asset packs:**

> Kenney. (n.d.) *Car Kit*. [3D asset pack]. CC0 1.0. Available at:
> https://kenney.nl/assets/car-kit (Accessed: 7 September 2026).

**Code we learned from, even without copying:**

> Makeev, S. (n.d.) *ArcadeCarPhysics*. [Source code]. MIT License. Available at:
> https://github.com/SergeyMakeev/ArcadeCarPhysics (Accessed: 7 September 2026).

The access date matters for anything online, because it is the honest answer to "was this the licence
when you used it". Note the licence in the citation too — it costs one clause and it shows the
licence was actually checked rather than assumed.

If we only read a repo and wrote our own version, it still goes in the reference list. Influence is
citable, and citing it is cheaper than being asked where a method came from and having no answer.

## Things I still have to check myself

Being honest about what I have not verified:

- Every Poly Pizza, OpenGameArt and Freesound item needs its licence read **on the page** at download
  time. I listed those sources as mixed because they genuinely are, and I have not opened individual
  files yet.
- ~~Each GitHub repo above needs its `LICENSE` file read before I copy any code.~~ Done on
  7 September 2026. Three are MIT. `Delt06/arcade-car-controller` declares no licence at all, so it
  is read-only for us and nothing gets copied from it.
- Licences do occasionally change. I checked these while writing this, so if we download something in
  October it is worth a glance rather than assuming this file is still right.
- **The repo is currently public, which contradicts the private-repo rule above.** Until that is
  fixed, the Asset Store and Pixabay cautions are live problems rather than precautions. Nothing from
  either source should be committed while the repo is publicly readable.

---

*Sources checked while writing this: kenney.nl, quaternius.com, poly.pizza, opengameart.org,
freesound.org, pixabay.com, fonts.google.com, developers.google.com/webfonts/faq, unity.com/legal,
and the GitHub repos listed above. Every link here was opened and confirmed working on the day I
wrote this. Licence summaries are my own wording, not copied text.*
