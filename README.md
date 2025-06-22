# FTK MultiMax Rework V2

This mod is a patched version (bugfixes and code reorganization) of the mod "FTK MultiMax Rework" by [justedm](https://next.nexusmods.com/profile/justedm?gameId=2887) \
That mod itself was already a rework of "For The King Multi Max" by [samupo](https://next.nexusmods.com/profile/Samupo?gameId=2887)

The concept remains the same: adventures with more than 3 players.

justedm ran all their tests with 5 players, I ran all of mine with 4. \
They ran all their tests on the Epic Games version. I ran all of mine on the Steam version. \
Needless to say, we've got some alright coverage now :D

**What has changed:**

- (See MultiMax Rework for changes between the original and the first rework)
- Bugfixes:
  - Exiting a session no longer forces you to restart the entire game
  - More to come, as soon as I can find the cause of some more bugs (please submit your own error logs, to help)

## How to install:

**Easy Method** (Recommended) \
Use the [Thunderstore Mod Manager](https://thunderstore.io/package/ebkr/r2modman/)
(Just press "Manual Download", then follow their instructions)

**Manual Method**

1. Download and install [BepInEx](https://for-the-king.thunderstore.io/package/BepInEx/BepInExPack_ForTheKing/) by following their guide
2. Download `FTK MultiMax Rework v2.dll` from the [Releases](https://github.com/justedm/FTK-MultiMax-Rework/releases/tag/v1.4) page or download the mod from [Thunderstore](https://for-the-king.thunderstore.io/package/edm/FTKMultiMaxRework/)
3. Leave the file in the `BepInEx\plugins` folder
4. Launch the game!

## Configuration:

**IMPORTANT:** The player number in the config should match _exactly_ the player count in your game. (Yes, I know "Max Players" is misleading).

I'm hoping to fix this at some point, but for right now, it is what it is

The mod does _work_ without the correct player amount configured, but some things (only visual, from what I've seen) get wonky.

**N.B:** The configuration file is only generated after the game is launched with the mod enabled. Therefore, you might have to start then exit the game to configure the player amount.

## Issues

If you encounter a bug or error at any point, please do leave a github Issue

For now, the "black screen" issue is pretty rampant. It's somewhat random when it happens. And I'm working to fix it. However, as for now, just keep at it, and at some point it'll _probably_ work.

Hildebrant's Cellar is unaffected, works perfectly fine.

Happy hunting, \
Polars Bear
