# FTK MultiMax Rework v2.1

This version is a continuation and improvement of [PolarsBear’s FTK MultiMax Rework v2](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2).  
It includes several new systems, fixes, and rebalancing changes aimed at making 4–5 player gameplay smoother and closer to a native experience.

This fork is maintained by **Obli**, and tested primarily on **Steam** with 4 players.
Feedback is greatly appreciated.

---

## What’s New in v2.1

### Major Reworks

#### 1. Enemy Spawn System
- Fully reworked enemy spawning and expansion logic.  
- Enemies are now **matched dynamically to the current player count** instead of being locked to 3v3 or 4v2 (bug in previous versions)
- The system currently **clones existing enemies** to fill additional slots for 4–5 player parties.  
- A future update will make enemies **spawn randomly based on biome type** for more encounter variety.

#### 2. Shop Quantity Balancing
- Shop items that originally had **quantity = 3** (e.g., *Godsbeard*, *Panax*) now automatically **scale to the player count**.  
- Items with **quantity = 2** now scale to **player count − 1**.  

---

### Other Improvements
- Fixed **missing battle stance UI** on the first combat round (previously caused by invalid enemy dictionary state).  
- Added **failsafes** for invalid or dead enemies to prevent `KeyNotFoundException` during combat.  
- Rebuilt encounter cleanup routines — no longer destroys UI, only clears data.  
- Improved **camera targeting**, **enemy status syncing**, and **HUD refresh** for 4+ enemies.  
- Reworked **combat end** handling (`ReturnToOverworld`) to cleanly disable UI and reset only logic-critical data.  
- Smoothed out **attack queue**, **fight order**, and **diorama target** management for expanded enemy groups.

---

## Planned for v2.2
- Procedural enemy selection based on **biome/environment**.  
- Scaling of **loot, gold, and EXP** to match total enemies.  
- Adaptive **shop pricing** for larger player counts. (Depending on how balanced it is currrently)  

---

## Installation

### Easy Method (Recommended)
Use the [Thunderstore Mod Manager](https://thunderstore.io/package/ebkr/r2modman/).  
Just press *Manual Download* and follow their setup instructions.

### Manual Method
1. Install [BepInEx Pack for For The King](https://for-the-king.thunderstore.io/package/BepInEx/BepInExPack_ForTheKing/).  
2. Download the latest `FTK MultiMax Rework v2.1.dll` from this fork’s [Releases](https://github.com/ObliDev/FTK-MultiMax-Rework-v2.1/releases).  
3. Place the DLL into your game’s `BepInEx/plugins` folder.  
4. Launch the game.

---

## Configuration

**Important:**  
The configured player count must **exactly match** the number of active players in your game session.  
This ensures that combat, rewards, and scaling remain consistent.

If the configuration file isn’t visible, launch and close the game once — BepInEx generates it on first load.

---

## Known Issues
- Occasional **black screen after loading** — typically resolves by restarting the session.  
- **AOE/group abilities** may only affect the first two enemies.
- Some UI elements may reposition incorrectly in 5-player encounters — purely visual.

---

## Previous Versions

This mod builds upon the work of multiple developers over time.

| Version | Author | Description |
|----------|--------|-------------|
| **Original MultiMax** | [samupo](https://next.nexusmods.com/profile/Samupo?gameId=2887) | First implementation of >3 player support |
| **Rework** | [justedm](https://next.nexusmods.com/profile/justedm?gameId=2887) | Code cleanup and stability fixes |
| **Rework v2** | [PolarsBear](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2) | Updated to support latest FTK version and fixed session crashes |
| **v2.1 (Current)** | [Obli](https://github.com/ObliDev) | Enemy spawn and shop rework, encounter/UI fixes |

---

## Changelog

### v2.1
- Reworked enemy spawn logic to scale with player count.  
- Adjusted shop item quantities to match player number.  

### v2.0
- Major rewrite of MultiMax Rework by PolarsBear.  
- Fixed crashes with >6 players and session restarts.  
- Improved stability and reorganization of encounter logic.

---

## Credits

- **Original Concept:** [samupo](https://next.nexusmods.com/profile/Samupo?gameId=2887) — *For The King Multi Max*  
- **First Rework:** [justedm](https://next.nexusmods.com/profile/justedm?gameId=2887) — *MultiMax Rework*  
- **Second Rework:** [PolarsBear](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2) — *v2 core improvements*  
- **Current Fork (v2.1):** [Obli](https://github.com/ObliDev) — *enemy + shop system rework*

---

Happy hunting,  
**by Obli & PolarsBear & All other Great Developers!**
