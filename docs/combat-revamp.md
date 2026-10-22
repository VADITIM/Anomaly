# Combat revamp: combos, charged hits, air and dodge

Status: design discussion, not implemented. Numbers are starting values. Open questions at the end need answers before planning.

## Why

The skill tree should unlock better combos and attack patterns, not just more damage. Today the player starts with a 3-hit ground combo, and the skill tree adds the 4th and 5th hits. That works, but combat needs more than a longer chain.

## Ground combo

- **Base:** 3-hit combo. This is what the player starts with.
- **Skill tree:** unlocks the 4th and 5th hits, as now.
- The tree is where superior combos and attack patterns unlock, so each new hit or pattern is a reason to spend a point.

## Charged hits (replaces the current charge-and-release)

The current charged attack charges and then releases. It goes away. The skill tree's charged moves become a timed input instead:

- **Input:** 1 hit, 2 hit, then hold for the third hit. Holding the third hit starts the different combo.
- **Windup:** the held third hit has a short windup of about **0.4 s** before it fires.
- **Perfect release:** releasing at about **0.3 s** into the windup is a perfect hit, at **100%** damage.
- **Late or early release:** any other release is a normal hit, at **90%** damage.

Open detail: the perfect window's tolerance is not set. A single frame window is very hard to hit; a small window (for example ±2–3 frames around 0.3 s) is the usual choice. Tune in playtests.

## Air attacks

- **Base:** air attack is a **2-hit slice combo**. This replaces the current air attack.
- **Skill tree:** air attacks get their own branch, separate from the ground tree. Starting ideas:
  - more airborne damage,
  - a 4-hit air combo,
  - air combo extensions, such as a finisher or a hover-slice.

## Dodge attacks

- A **dodge attack** is a single strike performed during a dash.
- It is one strike, not a combo. Its damage and reach are set separately from the ground combo so it reads as a reward for dodging, not a replacement for the combo.
- Base dodge attack is available without a skill point, so the mechanic is taught early. Skill tree upgrades can improve it.

## Dodge into jump

- Dodging into a jump is a **skill tree** unlock, not a base move.
- Candidates for the tree: an **air dodge** (a dash mid-air), and other movement techniques that combine dodge and jump.
- Air dodge should reuse the dash and air states, so it doesn't need a separate movement system.

## Skill tree branches (starting shape)

| Branch | Base | Unlocks |
| --- | --- | --- |
| Ground combo | 3 hits | 4th and 5th hits, combo patterns |
| Charged hits | timed third-hit input | perfect window and the charged combo |
| Air | 2-hit slice | airborne damage, 4-hit air combo, air finisher |
| Dodge | single dodge strike | dodge strike upgrades, dodge into jump, air dodge |

## Implementation notes

- Combat state lives in the player combat code (`Player.Combat.cs`). The combo chain, the charged hit and the air and dodge variants are separate states, so the tree can enable each one independently.
- The held third hit and the perfect window need a timer that starts at the windup and reads the release time. Keep the timing in frames or fixed time so it's consistent at any frame rate.
- Damage multipliers (100% perfect, 90% normal) belong with the hit data, not in the input code.
- Scale changes to the combo chain happen after the skill tree data exists, so the tree and the combat code change together.
- The Kio weapon and offhand work in `docs/kio-weapon-offhand.md` changes the arm animations for each combo state. Plan both with the same state list.

## Open questions

1. Perfect window tolerance: exact frames or a range around 0.3 s?
2. Does the charged third hit also exist on the air branch, or only on the ground?
3. Is the base dodge strike free, or does it need the first dodge skill point?
4. Is air dodge in the same branch as dodge into jump, or a separate unlock?
