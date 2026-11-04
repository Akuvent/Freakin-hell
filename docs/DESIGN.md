# Freakin' Hell - Design Doc (working title)

Status: Draft v0.1 (brainstorm, not yet prototyped)

## Pitch

A 2D stick-figure fighter where you grow your build between rounds. Each round takes place on a shrinking map: you level up by farming mobs, find stat items, and pick cards to shape your fighter. Local multiplayer, 2 players.

## Pillars

1. **Fights stay tight.** The map pushes players together, and impact frames make hits feel big.
2. **Every round is a fresh build race.** Cards, XP, and items reset or change each round, so there is always a reason to try a new route.
3. **Small and readable.** Stick figures, a few maps, and clear rules, so a three-person team can finish it.

## Core loop

1. Round starts. Mobs spawn and items are placed on the map.
2. Players farm XP and items while the map rots.
3. Players fight when they meet. Hits add percent; KOs remove stocks.
4. Round ends on a KO. The loser gets one free card. Then players pick cards between rounds.
5. Best of N rounds wins the match.

## Systems

### Map rot
- Edge tiles collapse on a timer, starting about 60 seconds into the round.
- Pushes players together without forcing a fight.
- Rot speed is a tunable; tune in playtests.
- Map can become empty. This is a fighting game, not a MOBA.

### Mobs
- Give XP only.
- Can be themed as viruses or glitches (see the glitch section below).
- Do not respawn during a round.
- Reset at the start of each round.

### XP and levels
- XP fills a level bar.
- Each level-up unlocks one card to choose and gives a small stat bump.

### Items
- Give stat boosts only (no cards, no XP).
- Capped per round to limit runaway stat leads.

### Cards
- Main build system. Each card changes one number or rule.
- Example cards: Heavy Hitter (heavy damage and knockback +30%), Long Fuse (light hitstop +0.05s), Blackout Ink (heavy impact frames last 2x longer), Quick Charge (faster charge meter), Extra Hop (+1 air jump), Thick Skin (percent gained -20%), Featherweight (knockback taken -20%), Stubborn (shorter move lock on hit).
- Card pools (endless mode): cards are grouped into rarity pools. The active pool rotates every few rounds, and useful cards are rarer and not all in the pool at once. Rotation is announced so players can plan around it.
- Good cards are building blocks, not raw power. Their value depends on combining them with other cards and using them at the right time, so a badly used block can make a build worse.
- Sockets: cards go into socket slots on the player's weapon. The number of slots is limited, so a build can only hold so many cards at once. More slots unlock later (for example, on level-ups). Swapping cards is allowed, so players can change their build between rounds.
- Socket cards: every 5 levels, the level-up offers a socket card (+1 socket). The player can take a skill instead if they need that more.
- Negative cards: rare cards that don't use a socket, like Negative editions in Balatro. They need a real drawback, or they're just free power. Limit how many a build can hold.
- Pity system: after a number of rounds without a useful card, the odds of a useful card rise in each offer. It increases luck, it doesn't guarantee a card. The odds reset when a useful card is picked.
- Card modding (later goal): each card is a JSON file (numbers, rarity, pool). Cards can call named actions from a fixed list in code (for example `"action": "extend_impact"`). Modders pick from the list; they never point to arbitrary code. Invalid cards are skipped with a logged error, not a crash.
- Card architecture: cards don't call each other. Each card declares tags (fire, heavy, air), a trigger (on_hit, on_heavy_hit, on_round_start), and a list of effects from a small set of primitives (modify_stat, extend_impact, apply_status). Interactions come from shared tags, stats, and statuses, so adding a card adds interactions automatically instead of requiring pairwise code. Stacking order is fixed (adds first, then multipliers), so results are predictable. Unique one-off cards can still use a named custom action, but they should be rare.

### Loser bonus
- The player who lost the previous round gets one free card.

### Camera (current choice, revisit in playtests)
- Map is about two to three screens wide.
- One camera follows both players and zooms out to fit them.
- If the players are too far apart, switch to split view; merge back when they get close.
- Split/merge transition: the screen cuts diagonally. Player 1's half slides down from above, Player 2's half slides up from below, a black separator line appears between them, and the camera shakes as if they fell in. Merging plays the same transition in reverse.

## Milestones

1. **Combat core:** Two players, light and heavy attacks, percent, stocks, hitstop, impact frames (reuse platformer-fighter code). Fixed arena with rot. KO ends the round.
2. **Build loop:** Card screen between rounds, 5 to 10 starter cards, loser bonus card.
3. **XP and mobs:** Mobs drop XP, level-ups give cards, per-round respawn.
4. **Map and items:** Full map (two to three screens), camera zoom and split, item drops with a cap.
5. **Balance and polish:** Card and stat tuning, sound, first art pass, team playtest.

Rule: do not start the next milestone until the previous one is fun.

## Post-v1 idea: fog of war and intel

- Map areas outside each player's vision radius are fogged. Mobs and items in fog are hidden.
- Players can run into each other by accident while farming.
- Intel cards reveal the opponent's position for a few seconds (cooldown, so it can't trivialize fights).
- Smoke-style cards block the opponent's vision.
- Local play: fog hides information only when each player has their own view (split view). On a shared screen, fog can only be a visual effect shared by both players, not hidden information.
- Hidden-information fog is therefore compatible with local play as long as split view exists. It does not need online play.

## Random events and gamble (from Discord)

- **Gamble between rounds:** a roulette runs between two rounds. The player can optionally wager one of their cards on it. Exact odds and outcomes are still open.
- **Special event levels between matches:** not rare, but not every match. They replace a normal match and rotate through a small set.
  - **Rising map:** the map rises faster than normal, and the first player to leave the screen loses. This reuses the rot system, so it's a good early candidate.
  - **Co-op level:** players have to work together on a shared goal. The goal and how it fits the versus game are still open.
- Events are opt-in for the wager and random for the level, so they add variety without changing the core loop.

## Cut for v1

- Online play
- Big roster (start with two fighters)
- Story or campaign
- AI opponents
- Many maps (start with one)

## Risks

- **Balance:** Stacking cards and stats can break fights. Keep the card count low and test early.
- **Camera:** Split view breaks the fight. Keep the map small until the camera works.
- **Scope:** Mobs, XP, items, cards, and rot are five systems. Build them one at a time.

## Open questions

- Round length target (2 to 3 minutes?)
- Best-of-N value (3? 5?)
- Socket rules: where do sockets live (weapon or fighter), how many at the start, what unlocks new ones, and does the loser's free card need an empty socket?
- Stat items: they don't use sockets, so do they need their own cap?
- Pity curve: how fast do the odds rise, and is there a cap? Is pity tracked per player or per pool?
- Skills vs cards: are skills a separate system (for example, active abilities), or are they cards too?
- Negative cards: what drawback is enough to balance a free slot?
- Endless mode: how long does it last, and does the rotation keep going with no round limit?
- Exact rot timing and speed
- Final title (Freakin' Hell is the working title; check Steam and search for collisions)

## Card presentation: syringes and poison (ideas, unsorted)

Team chat ideas, not committed yet.

- **Cards as syringes:** each card is a syringe. The card screen is "Choose your poison."
- **Sockets as injection points:** a socket is where a syringe goes into the fighter. Filling a socket means injecting the card, so the socket system and the syringe visual are the same thing.
- **Violent injection:** the fighter is restrained, and the syringe is injected into them. It's a stylized, horror-flavored moment, and it can play between rounds.
- **Rejection in combat:** the body can reject an injected card during a fight, which expels it and frees the socket. This gives cards a risk that plays out during the match.

Open questions for this section:
- When a card is rejected, is it gone for the match, returned to the pool, or back in the next round's offer?
- What triggers rejection: a timer, a number of hits, low stocks, or a random roll? It has to be readable to the player, so they know why it happened.
- Needles and injection imagery can affect age ratings and store listings. Should it stay stylized, or be toned down?

## Glitch aesthetic and events (ideas, unsorted)

Team chat ideas, collected here so they aren't lost. Nothing here is committed.

- **Glitch style:** digital, broken-looking visuals as the game's identity. Cheap to do with shaders and overlays.
- **Fourth-wall break:** a very strong attack can break the game's visuals for a moment (screen tearing, parts of the map shifting), similar to the Pac-Man kill screen. Reserve it for the strongest moves so it stays special.
- **Rot as glitch:** before an edge tile collapses, it flickers or corrupts. That also gives players a warning, so the collapse feels fair.
- **Mini events:** timed or triggered events during a round, such as a platform that teleports with a glitch effect. These should use the same trigger and effect system as cards, so they don't need separate code.
- **Mobs as bugs:** the farmable enemies are viruses or glitches. This fits the glitch theme and the horror side.

Open questions for this section:
- Which parts are cosmetic only (shaders, overlays) and which change gameplay (broken physics, teleporting platforms)? Gameplay changes cost much more.
- How often can the fourth-wall moment happen before it stops being special?

## Reference projects

- `Documents\platformer-fighter`: existing Godot 4 prototype with two local players, light and heavy attacks, percent, stocks, hitstop, and impact frames. Reuse its combat code for milestone 1. The `freakin-hell` repo itself is still empty apart from this doc.

## Title options

- Freakin' Hell (working title)
- Rot Rounds
- Rotting Arena
- Hellbound Brawl
