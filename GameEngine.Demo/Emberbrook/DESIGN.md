# Emberbrook: the next playable chapter

Design baseline, September 6, 2026.

The following milestone is substantially implemented. The subsequent implemented gameplay pass
is [A Place You Know](GAMEPLAY.md), September 7: authored village requests,
exploration discoveries, useful construction projects, and a chapter ending.
Treat the observations and proposed values below as historical; README.md
describes current rules and GAMEPLAY.md records the subsequent content pass.

Implementation update: the backpack/service panels, batching, Broken Crossing,
safe forest edge, tool/rich vein, smoked trout, spear, dialogue/journal, capped
combat bonus, entry/Continue, safe milestone autosave, and presentation pass are
now implemented. See README.md for current rules. Numerical values below remain
the original tuning proposals; human pacing and comprehension targets are still
unverified. No additional region or optional boss-rematch system was required
by this milestone.

## Player promise

Become the adventurer a small village depends on. Learn useful trades, prepare
for short expeditions, discover what happened beyond the village, and bring home
something that changes how you play or how the village looks.

Preserve the relaxed click-to-act rhythm. Gathering should be pleasant preparation;
combat should reward clear decisions and readable movement. Aim for a satisfying
45–60-minute first chapter with optional reasons to revisit it. This is a pacing
target to test, not a measurement of the current game or an implementation estimate.

## What the current game tells us

The existing four quests connect five skills, three regions, and two bosses.
Cooking already gives fishing a practical use; the forest beacons give woodcutting
a purpose; the charm introduces an equipment choice. These connections are the
strongest foundation for further design.

The progression currently has several limits:

- Most new objectives come from returning to Elin through one quest sequence.
  Players have limited opportunities to choose their next goal.
- Copper equipment ends at a sword and shield. Most skill levels simply speed
  up gathering or increase damage, so they promise few new activities.
- The four quest rewards total 300 coins, while the permanent equipment purchase
  costs 20 coins. Food provides ongoing spending, but the village needs additional
  useful things to buy. Increasing rewards alone will not address this.
- Each additional 30 Combat XP adds damage indefinitely. Enough ordinary-enemy
  grinding can eventually erase boss mechanics. Tune progression around the
  intended encounter experience before adding stronger enemies.
- Gathering goods uses a shared 12-unit pack, making repeated resource collection
  compete with food. That can become travel friction without an interesting choice.
- The recent mouse-release regression demonstrates that input reliability is part
  of the game design. Test complete press/release gestures, including runner-generated
  keys, when changing controls.

These are observations from the implementation. Actual pacing, difficulty, and
player preferences still need human playtests.

## Principles for new content

1. An upgrade should open a useful possibility: a recipe, route, preparation
   choice, or tactical option. Small numerical bonuses can support that reward.
2. An expedition should answer three questions: what am I seeking, what should
   I bring, and when will I head home?
3. Preparation may make mistakes more forgiving. It should not be required to
   survive an otherwise unreadable encounter.
4. Every major quest should leave a visible result: a repaired crossing, a working
   bell, a busy feast table, or a lit trail.
5. Show the action, cost, and outcome before a transaction. One click starts an
   activity; releasing the mouse never cancels it. X or a new destination stops it.

## Recommended milestone: A Village Worth Returning To

Finish one complete loop in the existing world before expanding the map:

**Choose a village project → gather supplies → craft or trade → finish the project
→ use its reward on an expedition → return with a new purpose.**

### First delivery: a proper backpack and workbench

Replace the long inventory text list and cycling bank selector with a compact
item grid. Selecting an item shows its name, quantity, purpose, and available
actions. Keep health, current activity, and the tracked objective visible.

Use separate Backpack, Skills, and Journal views in the sidebar. Opening any view
must preserve walking and gathering. Combat continues while panels are open;
health, threats, Eat, and Stop remain accessible. Avoid adding pause behavior
implicitly through menus.

At a service, show an explicit panel:

| Service | Player decisions | Required feedback |
|---|---|---|
| Forge | Select recipe; craft one or a chosen quantity | Owned / required materials, coin cost, resulting item |
| Bank | Select item; deposit or withdraw quantity; deposit all | Carried and stored quantities, capacity remaining |
| Bram | Buy food or an upgrade; sell selected goods | Unit price and total; quest reservations identified |
| Cooking fire | Choose recipe and quantity | Healing or preparation effect; ingredients consumed |

Start with the existing recipes and items. A queued batch stops on insufficient
materials, X, or a new order; it consumes ingredients only as each item is made.
Full health never consumes food. Never silently sell equipment or reserved quest
items. Cooking and smelting must remain usable with a full pack when the recipe
itself frees the required space.

**Inventory experiment:** stack ore, logs, and raw fish to five per slot; keep
prepared food at one per slot. Equipment and quest tokens use separate storage.
Keep 12 slots initially. This lets a player bring back a useful material haul
while retaining an expedition food decision. Show both quantity and occupied
slots. Verify that resource collection does not become endless waiting: if it
does, shorten batches or increase yields before increasing capacity again.

Completion criteria: a new player can discover how to make a sword, bank a
specific item, and cook a batch without reading the README or knowing a hotkey.

### Second delivery: The Broken Crossing

Add a short village project offered by Bram after The Copper Promise. It can be
pursued alongside The Sunken Bell. This establishes player choice without needing
a general quest-board system.

The western forest edge becomes accessible at this point for woodcutting. Keep
the deep forest and Hart quest gated until their existing story prerequisites.
Reuse the forest region, with a clearly blocked passage separating safe gathering
from the dangerous interior. Explain the restriction in the world.

Bram wants to reopen a damaged footbridge near the fishing and woodland routes.
The player chooses either to supply **four ash logs and one copper bar**, or pay
**45 coins** for village supplies. Both are provisional costs. Display both
options before accepting anything; consume the complete chosen payment once.

Repairing it visibly changes the crossing and opens a shorter route between
existing activities. Ensure the route is actually shorter when measured, while
the unrepaired route remains valid. The material route gives woodcutting and
smithing a shared purpose; the coin route gives fishing income and quest rewards
a useful outlet. The same reward is available through either play style.

Give Bram a small character arc: he initially treats the crossing as a delivery
problem, then admits it cut him off from someone he visits. A few short exchanges
and a visible return trip will do more than a long exposition panel.

Completion criteria: both routes work; the bridge cannot charge twice; its state
persists; a player can explain why they chose their payment and uses the shortcut.

### Third delivery: meaningful preparation

Add a small set of choices with distinct purposes. These are first tuning values,
not a promise that every item should ship together.

| Reward | Proposed source | Why the player wants it |
|---|---|---|
| Reinforced gathering tool | 2 copper bars + 30 coins | Opens one richer resource node; avoid stacking a large speed bonus |
| Smoked trout recipe | Village project follow-up | Carry two portions per slot, each healing less than grilled trout |
| Ash spear | Ash logs + copper bars | Trades the shield slot for a two-tile attack reach |

Implement the tool and its node first. Add the recipe only after stack rules and
food selection are clear. Prototype the spear last: two-tile attacks require
line-of-sight, pursuit, and boss tests so it cannot attack through obstacles or
make an encounter harmless from outside its trigger range.

Keep the existing sword and shield a good choice. The spear offers positioning
flexibility at the cost of protection; it should not become a mandatory replacement.
Allow equipment changes in safety, show the tradeoff numerically, and disallow
mid-attack swapping. Do not add a full equipment rarity ladder yet.

## Progression and combat pass

Before extending the chapter, map the expected skill and equipment state at each
quest. Use skill levels to reveal the next unlock in the Skills view. For this
chapter, place a few visible milestones within the first five levels rather than
promising a huge mostly empty level range.

Prototype slowing combat damage growth to one point per two levels, with a chapter
bonus cap. Check the first mossling fight and both bosses before choosing final
values. A cap must be visible to players; never display a level-up benefit that
the combat formula ignores. Keep the existing combat model until this experiment
has comparative playtest results.

Keep ordinary fights brief and legible. Introduce one enemy behavior at a time:
stationary mosslings teach attacking and food; sentinels teach pursuit and retreat;
wolves test preparation; bosses test movement. Avoid raising health as the default
way to increase challenge.

Boss encounter targets:

- First warning gives a new player enough time to recognize the shape and move.
- A reasonably equipped player can survive one mistake and recover with food.
- A successful ordinary-gear attempt lasts roughly 30–60 seconds and demonstrates
  the boss mechanic several times. Measure this; current automated wins alone
  do not establish a satisfying encounter.
- Clearly signal the Hart's protected state before the player spends an attack.
- Give victory a short, distinct sound and visual moment, then make the next
  objective obvious. Preserve permanent boss defeat in the normal world.

Keep death forgiving while balancing. A distant corpse run would add frustration
and obscure whether the core encounter is fun. Optional boss rematches can come
later through a deliberate challenge interaction with separate rewards.

## Make the world feel inhabited

Apply these alongside the milestones, in small passes:

- Distinct feedback sounds for selecting, gathering, crafting, damage, danger,
  and completion; provide a mute control and retain equivalent visual cues.
- Four-direction character facing and a brief gathering/attack pose. Check that
  animation timing matches the real action and does not delay input.
- NPC dialogue options that distinguish talking, trading, and turning in a quest.
- A Journal with completed quests, active objectives, and one tracked goal.
  Return-to-Elin objectives should name her location.
- Visible quest consequences for the existing bell, feast, and beacons.
- New Game / Continue at entry, plus autosave at safe milestones. Preserve manual
  saving. A failed save must remain visible and must not claim success.

Keep implementation details out of the interface. Do not describe save versions
or simulation timing to players.

## Delivery order and scope control

| Order | Playable delivery | Gate before moving on |
|---|---|---|
| 1 | Backpack, service panels, recipe batches | Item conservation and complete desktop click flows pass; a newcomer can use them |
| 2 | Broken Crossing, parallel journal tracking, safe forest edge | Both payment paths and the shortcut work; one visible persistent village change |
| 3 | Tool upgrade and richer node; coin/XP tuning | At least two worthwhile spending goals; early combat stays enjoyable |
| 4 | Food tradeoff, optional spear prototype, presentation pass | Players can describe a preparation choice; bosses remain fair with each supported loadout |
| 5 | Consider a new region | Existing chapter meets pacing and comprehension targets |

Keep new systems in the demo game layer. Use small item/recipe definitions where
multiple screens need the same facts, and extend the real Engine harness. Do not
build a generic quest scripting engine, procedural world generator, multiplayer,
or a broad crafting tree for this milestone. Save migrations are not required.
The current save format can change with a clear fresh-start message.

## Playtest plan

Run three short human sessions, ideally including someone unfamiliar with the
controls. These are formative checks, not statistically conclusive results.

1. **Fresh start:** observe the first ten minutes without coaching. Record first
   successful interaction, first crafted item, wrong clicks, and requests for help.
2. **Choose a goal:** offer the bell quest and bridge project together. Ask what
   the player plans to do next and why. Afterward ask what the reward changed.
3. **Prepare and fight:** let the player choose supplies and face a boss. Record
   warning recognition, damage taken, food used, deaths, fight duration, and how
   often input behaves differently from their expectation.

Targets for the next iteration:

- A first useful crafted item within about five minutes without instructions.
- At least two understandable goals available after the first quest.
- A useful unlock or visible world change approximately every five to ten minutes.
- No involuntary action cancellation on mouse release, panel changes, or hover.
- No task whose main difficulty is repeated banking trips or finding the right button.
- After a defeat, the player can name what they would do differently.

If these fail, fix the relevant loop before adding another map. Record observed
values next to these targets after each session; change the targets when the
experience justifies it.
