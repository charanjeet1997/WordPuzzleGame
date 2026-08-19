# Aurora Words — Third-Party Licences

Everything bundled with this project, what it is licensed under, and what must travel with
it if the source is redistributed or sold.

Last reviewed: 2026-08-19.

---

## 1. Word definitions — WordNet 3.1

**Files:** `Assets/WordPuzzle/Resources/word_definitions.json`,
`Assets/WordPuzzle/Resources/word_definition_overrides.json`

**Source:** Princeton University WordNet 3.1 — <https://wordnet.princeton.edu/>

**Licence:** WordNet Licence (permissive, commercial use allowed).

**Obligation:** the copyright notice must be distributed with the data. It is kept in
`Assets/WordPuzzle/Resources/word_definitions_LICENSE.txt` — **do not delete that file.**

Only glosses for words in this game's word lists were extracted; usage examples were
stripped and senses reordered by WordNet's own tag counts. Hand-written entries in
`word_definition_overrides.json` are original to this project and carry no WordNet claim.

---

## 2. Word lists

**Files:** `Assets/WordPuzzle/Resources/word_list.txt`,
`word_list_targets.txt`, `word_list_target_exclude.txt`

Derived from a public word corpus, then filtered against WordNet and a frequency ranking.
The resulting lists are factual data (word inventories are not themselves copyrightable) and
are distributed with this project.

---

## 3. Background music

**Files:** `Assets/WordPuzzle/Audio/Music/*.wav`

**Source:** "Royalty free game music loops" by **Pudgyplatypus**, via OpenGameArt.org —
<https://opengameart.org/content/royalty-free-game-music-loops>

**Licence:** CC0 1.0 Universal (public domain dedication) —
<https://creativecommons.org/publicdomain/zero/1.0/>

No attribution required and commercial use is permitted. Provenance is recorded in
`Assets/WordPuzzle/Audio/Music/CREDITS.txt` as a record, not an obligation.

---

## 4. Sprites and icons

**Files:** `Assets/WordPuzzle/Sprites/**`

Created for this project (several generated programmatically — buttons, pause, shuffle,
hint bulb, onboarding marker). No third-party rights.

---

## 5. Build-time tooling — not shipped

Used to *produce* the data above; no part of these is included in the game or this
repository:

- **Princeton WordNet 3.1 database** (`wn3.1.dict.tar.gz`) — parsed to build the definitions
- **Norvig `count_1w.txt`** word-frequency list (Google Web Trillion Word Corpus) — used to
  rank word commonness while filtering the target list

---

## 6. Framework code under `Assets/ExternalFramework/`

Written by the project author and reused across projects: ActionSystem, AnimationSystem,
CameraManager, CanvasUtility, ClassCreator, CommanTickManager, ComponentCopier,
EventManagement, Extensions, Factory, FlowSystem, GameStateFramework, ServiceLocator,
SimpleGenericStateMachine, TaskSystem, UISystem, Utilities, ViewModel, WebServices, World,
and the EnhanceScroller* controller wrappers.

The game depends on `ServiceLocator`, `CommanTickManager`, `ViewModel` (data binding),
`Factory`, `UISystem` and `World`, so those ship with any sale.

### Must be deleted before distributing

| Folder | Why |
|---|---|
| `ExternalFramework/Best HTTP (Pro)/` | Commercial Unity Asset Store package (BestHTTP), including its documentation PDF. Redistribution is prohibited by the Asset Store EULA. **No game code references it**, so deleting the folder costs nothing. |

Nothing under `Assets/WordPuzzle/Scripts/` references BestHTTP or EnhancedScroller —
verified 2026-08-19.

---

## 7. This project's own code

`Assets/WordPuzzle/**` excluding the third-party items above.

Licence: see `LICENSE.md` — a proprietary single-seat licence. Buyers may build unlimited
commercial games from it; they may not redistribute or resell the source.

---

## Pre-sale checklist

- [ ] Delete `ExternalFramework/Best HTTP (Pro)/` (commercial package, unused by the game)
- [ ] Replace `google-services.json`, AdMob unit IDs and any API keys with placeholders
- [ ] Never include the keystore or its passwords
- [ ] Keep `word_definitions_LICENSE.txt` alongside the definitions data
