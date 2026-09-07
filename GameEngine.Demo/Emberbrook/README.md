# Emberbrook — a village worth returning to

A single-player click-to-act RPG with three connected regions, four adventure
quests, four resident requests, three construction projects, six discoveries,
five skills, original pixel art, and sound
feedback. Select Emberbrook in the demo menu, then choose **New adventure** or
**Continue**. This is a compact playable chapter; the design plan's 45–60-minute
pacing target still needs human playtesting.

## Controls and interface

- Click a destination to walk or a site to approach it. Release the mouse: the
  action continues. Click elsewhere or press **X** to stop. Mouse release can
  generate Space in the desktop runner, so Space deliberately has no RPG binding.
- **M** or the Repeat button toggles repeated mining, fishing, and woodcutting.
  Resources replenish between actions; collection stops when the next yield
  cannot fit. A gold bar shows action progress.
- **E** or Eat consumes suitable food when wounded. It consumes nothing at full
  health. Health, Eat, Stop, Save, and Load remain accessible with panels open.
- **Backpack** shows twelve slots and item details. Ore, raw trout, and logs stack
  to five; smoked trout stacks to two. Bars, rations, and grilled trout use one
  slot per item. Equipment and quest tokens use separate storage.
- **Skills** shows levels, cooking milestones, and the combat bonus cap.
  **Journal** shows adventure progress and tracks the main story or bridge project.
  Its section button cycles through requests, the fieldbook, and construction;
  Previous / Next browses entries. Talk to Nell by the western fire or Orin north
  of the forge for optional deliveries.
  Tabs and hover do not cancel actions or pause combat.
- **T** switches the earned charm while idle. Equip the spear or sword through
  Backpack while idle and away from enemies. **B** sells surplus fish beside Bram.
- **S** saves, **L** loads, **Q** returns to the demo menu. The Sound button mutes
  or enables feedback cues. New Game and Continue are available on RPG entry.

## Village services

Clicking Elin or Bram opens their service choices. Elin offers dialogue and an
explicit Accept / turn in button. Bram offers dialogue, purchases, selected-item
sales, and bridge choices. Forge and cooking-fire clicks start the selected recipe.

| Service | Actions |
|---|---|
| Forge | Browse recipes, inspect costs and owned supplies, select a batch of 1/5/10, then Smelt or Forge |
| Cooking fire | Browse grilled and smoked trout recipes; select a batch |
| Bank | Rest to full health; select a stored item type; deposit/withdraw 1/5/10 or deposit everything |
| Bram | Buy rations for 3 coins each; sell selected supplies; discuss or repair the crossing |

Clicking the forge or fire opens Backpack and makes its selected recipe and
batch quantity, walking into range first when needed. Recipe arrows choose the
output; click the workstation again or press the labeled Smelt, Cook, Smoke, or
Forge button to begin. That button works on its first click from any tab. The
forge and fire remember separate recipes. Equipment recipes always make one item.

Crafting consumes ingredients only when each item completes. A new order or X
cancels the remainder. Missing materials or insufficient output space stop the
batch without partial payment. Equipment cannot be crafted and charged twice.

| Recipe | Cost | Benefit |
|---|---|---|
| Copper bar | 3 ore | Equipment and bridge material |
| Copper sword | 2 bars | Base attack increases from 2 to 5 |
| Copper shield | 2 bars + 20 coins; sword required | Reduces incoming damage by 2, minimum hit 1 |
| Reinforced tools | 2 bars + 30 coins | Opens the rich copper vein in the northeast |
| Ash spear | 4 logs + 2 bars | Two-tile straight-line reach; stows the shield |
| Grilled trout | 1 raw trout | Heals 12/14/16 as Cooking reaches levels 1/2/3 |
| Smoked trout | 1 raw trout + 1 log; repaired crossing required | Heals 8; two portions fit in one slot |

A spear cannot strike through a blocked intermediate tile or around a corner.
Changing weapons is prohibited during an activity, a marked attack, or within
six tiles of a living enemy. Both weapons use base attack 5; the shield tradeoff
makes spear positioning useful without making it a direct damage upgrade.

Fish sell for 2 coins raw, 5 grilled, or 4 smoked. Ore and logs sell for 1 coin;
bars sell for 3. Rations cannot be resold. Both selected sales and the fish-sale hotkey reserve accepted delivery supplies
in addition to active bridge materials and the feast's three grilled trout.
Bram's item panel displays the reserved quantity. Eating and crafting can still
use those supplies; abandoning a delivery releases its sale reservation. Transactions report actual
quantities and costs, including partial purchases when capacity or coins run out.

## The Copper Promise

Speak to Elin in the village square and accept her task. Gather six copper ore,
make two bars and a sword at the forge, then defeat three mosslings. Return to
Elin and turn in the quest for 40 coins. Copper seams yield one ore and 10 Mining
XP with four seconds of regrowth. The reinforced-tool vein yields three ore and
20 XP, then takes six seconds to replenish.

Mosslings have ten health, attack in melee, and replenish after twelve seconds.
Kills grant five coins and 15 Combat XP. Combat levels occur every 30 XP; the
attack bonus increases every two levels and caps at +4 for this chapter. The
Skills view displays the cap. Other gathering skills shorten action time as
their levels rise, with a minimum duration.

## The Broken Crossing

After Copper Promise, Bram offers this project independently of the bell quest.
The southern gate now leads to the safe forest edge, where ash trees are available.
The deep trail remains visibly sealed until Elin grants the Ashen Trail quest.

Choose **four logs and one bar**, or **45 coins**, at Bram's panel. Incomplete
payments consume nothing. Repair opens a northern crossing at brook row four,
shortening travel between the village and copper hills. The bridge changes
visually and remains repaired across visits and saves. Bram also teaches smoked
trout. His dialogue explains whom the washed-out route separated him from.

The Journal can track this project while the adventure quests remain active.

## The Sunken Bell

Accept Elin's next task after Copper Promise. Forge a shield or prepare a spear,
and bring food. The eastern gate leads to the Sunken Halls; the western dungeon
gate returns to the village.

Reed sentinels have 18 health and pursue intruders within six tiles, leashed to
seven tiles from home. They attack for four damage every 1.2 seconds when adjacent
and replenish after twenty seconds. Heal between encounters, particularly when
using the spear without shield protection.

The Bell Guardian has 60 health. Within seven tiles, it periodically marks a 3×3
patch around your current position. Leave that patch before the eight-damage
shockwave after 1.4 seconds. Recovery shortens below half health. The patch stays
in place, so move at least two tiles away, then click the guardian to attack again.

After victory, collect the bell from its altar and return it to Elin for 80 coins
and 30 Combat XP. The restored bell appears in the village. Boss defeat and the
quest reward are permanent and cannot be farmed by re-entering.

## River's Bounty

After the bell quest, accept Elin's feast task. Bring three grilled trout and a
river pearl for 60 coins and a River Charm. She takes neither offering until both
are ready. A feast table appears in the village after completion.

Fishing is available from the start. Each catch yields one trout and 10 Fishing
XP; shoals replenish in three seconds. At five successful catches, you find the
pearl, which takes no slot and does not return after quest completion. Cook at
the western fire for eight Cooking XP per fish; levels rise every 24 Cooking XP.

The charm starts as **Might**, adding two attack damage. **Shelter** reduces
incoming hits by one and stacks with an active shield. Damage always has a
minimum of one. Choose your charm while idle and outside marked attacks.

## The Ashen Trail

After the feast, Elin opens the deep forest. Cut six ash logs and light three
beacons with two logs each. Trees grant ten Woodcutting XP per log and regrow in
four seconds. Beacon flames persist and cannot charge twice.

Ash wolves have 24 health and pursue nearby players. The Briar Hart has 80 health
and is visibly ROOTBOUND until all three beacons burn. Its root attack marks a
cross extending two tiles along each axis. Move away from the marked row and
column before the roots strike after 1.5 seconds. Attack frequency increases
below half health. Defeat it and return to Elin for 120 coins and 60 Combat XP.

Both bosses have distinct warning and victory feedback, including glowing cast
animations. Four-frame directional clips distinguish walking, mining, chopping,
fishing, cooking, sword attacks, and spear attacks. Water flows, fish ripple,
trees sway, fires flicker, NPCs idle, and creatures breathe and walk. Ambient
clips use staggered phases. Chimneys smoke, butterflies drift through the village,
forest motes glow, and dungeon torches flicker. These details are decorative and
retire with the region. Short hit and gathering effects use a fixed pool. Damage/healing numbers and level-up
cues remain visual even when sound is muted or no audio backend is available.

## Village requests and discoveries

Nell and Orin each offer two one-time deliveries. Click a resident, browse with
Request arrows, then Accept request. Return with the full carried payment and
click Hand in. Previously gathered materials count. One optional delivery may
be active alongside the adventure and bridge project. Abandon it freely through
the resident or Journal and accept it again later. Incomplete hand-ins take nothing.

| Request | Unlock | Payment | Reward |
|---|---|---|---|
| Nell: Supper for Two | Arrival | 2 grilled trout | 14 coins; supper bowls |
| Orin: A Proper Delivery | Copper Promise | 2 bars + 4 logs | 16 coins; Bram's repaired wheel |
| Nell: Trail Lunches | Feast and crossing | 3 smoked trout | 15 coins; lunches at the southern gate |
| Orin: The Last Repairs | Ashen Trail turn-in | 3 bars + 6 logs | 24 coins; a village sign dedicated to the adventurer |

Six landmarks fill the Journal's fieldbook: Flood Mark, Nell's Old Float,
Quarry Marks, the Bellmaker's Name, the Lost Way, and the Quiet Spring. Click to
approach and inspect them. Undiscovered pages offer broad location hints; entries
and quest tokens take no backpack space. Show the float to Nell through Talk to
hang it by the fire. The bellmaker's inscription appears on the restored bell
even when found after the bell quest. The spring blooms after the Hart falls.

The Lost Way requires the deep trail open and Woodcutting 3. Clearing its fallen
trunk opens a permanent passage through the western forest hedge. The original
southern passage stays usable. The shortcut does not bypass beacons or quest gates.
Find all six places, then use the fieldbook's action beside Elin to earn the
Keeper of the Paths title and an explorer's banner in the square.

## Construction and homecoming

At Bram, open **Construction projects**, or use the Journal's Construction
section. Browse the project and its requirement, then Build with Bram. Construction
uses a single complete payment and cannot charge twice.

- **Fishing jetty:** after the crossing, pay 4 logs, 1 bar, and 25 coins. The jetty
  by the northern crossing requires Fishing 3 and yields two trout and 20 XP per
  catch, with five seconds of replenishment. Repeat works as at ordinary shoals.
  If both fish cannot fit, neither is collected. Building before Fishing 3 is
  allowed; ordinary shoals remain available for training.
- **Old return gate:** after the Ashen Trail turn-in, pay 2 bars and 120 coins.
  The gate near the Hart's clearing takes you one way to the village. Click while
  idle and safe; the character approaches and travels when adjacent. Marked
  attacks or a living enemy within six tiles prevent use. Rejected clicks do not
  queue an escape for later.

After returning to Elin with the Hart defeated, choose **Join the homecoming**.
The celebration acknowledges your actual completed work and gives the chapter a
clear ending. Skip / return to village and View unfinished work both resume play.
The celebration pauses the world after requiring safety. Replaying it gives no
additional quest rewards. Optional requests, construction, discoveries, and normal
gathering remain available after the chapter ends.

## Recovery and saving

Defeat brings you home at full health, costing up to five coins. Items, skills,
equipment, and quest progress are retained. Living enemies reset their health.
There is no corpse run.

Desktop progress is saved at
`LocalApplicationData/GameEngine/Emberbrook/save.json`. Safe village milestones
autosave, including quest turn-ins and bridge repair. Forest/dungeon milestones
are saved automatically on return to the village; manual S works during an
expedition. The persistent save status reports failures with a retry instruction.
A failed save does not erase progress in memory. Continue failures leave New
adventure available and explain the problem in the message area.

Version 6 saves contain all chapter progress, inventory and bank quantities,
equipment, region, accepted/completed requests, discoveries, construction,
resident acknowledgements, fieldbook award, and homecoming progress. Older versions require a fresh adventure; no migrations
are maintained. Unknown fields, invalid quantities, and impossible positions are
rejected before changing the world. Writes use a unique temporary file followed
by replacement. A failed write preserves the previous file.

Loading cancels activity and resets resource and ordinary-enemy timers. Permanent
boss defeats, beacons, the bridge, and quest rewards remain. Batch orders, sidebar
selection, repeat preference, and mute are session state. Browser disk saves
report that persistent storage is unavailable on that host.

## Structure and verification

The `EmberbrookWorld` partials contain game rules; item/recipe vocabulary lives
in `EmberbrookChapter.cs`. Scene partials handle panels, ordinary sprite/text
entities, presentation cues, and persistence UI. Rendering remains in Core's
RenderSystem. No engine component or schema was added for this chapter.

Each region is 24×15 tiles. Region and persistent world changes retire map
entities before rebuilding; floating feedback uses a fixed pool. Original art
and short PCM sound cues are generated by `tools/EmberbrookArt/Program.cs`:

```bash
dotnet run --project tools/EmberbrookArt
```

The real Engine headless tests cover the adventure quests, both bridge payment
routes, route shortening, the safe forest boundary, full-pack transactions,
recipe batching/cancellation, tool unlocks, smoked food, spear reach and walls,
both boss loadouts, item reservation, bank selection, panel interactions,
complete desktop click/release gestures, frame advancement and sprite-sheet geometry,
action clip switching and transient effect cleanup, Continue, autosave failure, save validation,
request acceptance/abandonment, additive sale reservations, all six discoveries,
shortcut distance and save positions, atomic jetty catches, construction payments,
safe return-gate use, homecoming skip/replay, and persistent world changes. Rendered previews cover the entry screen, forge,
bank, journal, restored village, forest edge, and boss arena.

See [DESIGN.md](DESIGN.md) for the design rationale and human playtest protocol.
Automated success does not establish the planned first-session duration or
new-player comprehension; those remain playtest questions.

The implemented content pass [A Place You Know](GAMEPLAY.md) records the design
and playtest targets for village requests, discoveries, improvements, and the ending.
