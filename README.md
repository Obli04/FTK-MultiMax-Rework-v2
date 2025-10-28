# FTK MultiMax Rework v3

This version is a continuation and improvement of [PolarsBear’s FTK MultiMax Rework v2](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2).  
It includes several new systems, fixes, and rebalancing changes aimed at making 4–5 player gameplay smoother and closer to a native experience.

This fork is maintained by **Obli**, and tested primarily on **Steam** with 4 players.
Feedback is greatly appreciated.


## What’s New in v3

### Major Reworks

#### 1. Enemy Spawn System
- Fully reworked enemy spawning and expansion logic.  
- Enemies are now **matched dynamically to the current player count** instead of being locked to 3v3 or 4v2 (bug in previous versions)
- The system currently **clones existing enemies** to fill additional slots for 4–5 player parties.  
- A future update will make enemies **spawn randomly based on biome type** for more encounter variety.

#### 2. Shop Quantity Balancing
- Shop items that originally had **quantity = 3** (e.g., *Godsbeard*, *Panax*) now automatically **scale to the player count**.  
- Items with **quantity = 2** now scale to **player count − 1**.  

### 3. New Hildebrant Difficulties

Hildebrant’s Cellar now includes **six custom difficulty levels** for extended replayability and smoother progression:

| Order | Name | Description |
|-------|------|--------------|
| 1 | **Apprentice** | Simplified version for new players |
| 2 | **Novice** | Slightly easier than default |
| 3 | **Journeyman** *(default)* | Standard FTK balance |
| 4 | **Master** | Moderate challenge increase |
| 5 | **Grandmaster** | Enemies scale faster and hit harder |
| 6 | **Godlike** | Extreme difficulty for optimized parties |

Each new tier modifies enemy health, gold rewards, and other parameters to create a more continuous difficulty curve from *Apprentice* → *Godlike*.



## Planned for v3.1
- Procedural enemy selection based on **biome/environment**.  
- Scaling of **loot, gold, and EXP** to match total enemies.  
- Adaptive **shop pricing** based on feedback.  
- More difficulties for other game modes.

## Installation

### Easy Method (Recommended)
Use the [Thunderstore Mod Manager](https://thunderstore.io/package/ebkr/r2modman/).  
Just press *Manual Download* and follow their setup instructions.

### Manual Method
1. Install [BepInEx Pack for For The King](https://for-the-king.thunderstore.io/package/BepInEx/BepInExPack_ForTheKing/).  
2. Download the latest `FTK MultiMax Rework v3.dll` from this fork’s [Releases](https://github.com/Obli04/FTK-MultiMax-Rework-v3/releases).  
3. Place the DLL into your game’s `BepInEx/plugins` folder.  
4. Launch the game.


## Configuration

**Important:**  
The configured player count must **exactly match** the number of active players in your game session.  
This ensures that combat, rewards, and scaling remain consistent.

If the configuration file isn’t visible, launch and close the game once — BepInEx generates it on first load.


## Known Issues
- Occasional **black screen after loading** — typically resolves by restarting the session.  
- **AOE/group abilities** may only affect the first two enemies.
- Some UI elements may reposition incorrectly in >4-player encounters — purely visual.


## Previous Versions

This mod builds upon the work of multiple developers over time.

| Version | Author | Description |
|----------|--------|-------------|
| **Original MultiMax** | [samupo](https://next.nexusmods.com/profile/Samupo?gameId=2887) | First implementation of >3 player support |
| **Rework** | [justedm](https://next.nexusmods.com/profile/justedm?gameId=2887) | Code cleanup and stability fixes |
| **Rework v2** | [PolarsBear](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2) | Updated to support latest FTK version and fixed session crashes |
| **v2.1 (Current)** | [Obli](https://github.com/ObliDev) | Enemy spawn and shop rework, encounter/UI fixes |


## Credits

- **Original Concept:** [samupo](https://next.nexusmods.com/profile/Samupo?gameId=2887) — *For The King Multi Max*  
- **First Rework:** [justedm](https://next.nexusmods.com/profile/justedm?gameId=2887) — *MultiMax Rework*  
- **Second Rework:** [PolarsBear](https://github.com/PolarsBear/FTK-MultiMax-Rework-v2) — *v2 core improvements*  
- **Current Fork (v3):** [Obli](https://github.com/ObliDev) — *enemy + shop system rework*

