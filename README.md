# FsOptimizer

FusionOptimizer (FsO) is a mod that aims to make a Fusion server hoster's day *slightly* better, with features such as auto-clean, quick level reload, memory clean, and more!

## Features

All of the features listed below can be found in the FsOptimizer option in the BoneMenu. To access the BoneMenu, hold the Quick Menu button on your controller (B/Y buttons on a Quest controller), release it while hovering over the "Preferences" button, then click the "BoneMenu" Button.

### Server Cleaner

Similar to Fusion's built-in Admin Cleanup tool, the Server Cleaner will despawn everything in the game to quickly and effectively remove spawnables. By using FsO's Server Cleaner, it will despawn every spawnable using Fusion's Admin Cleanup tool, **AND** clean any extra spawnables that Admin Cleanup fails to despawn, like the Dark Fountains made by [Siloquenn](https://mod.io/g/bonelab/u/sileqoenn)'s [Fountain Maker](https://mod.io/g/bonelab/m/deltarune-the-fountain-maker) mod.

### Level Reloader

The Level Reloader does exactly what it says it does: it reloads the current level you are in. This is meant to be used when FsO's Server Cleaner doesn't clean everything. (Although, it can be used for other things too, if you'd like.)

### Auto Cleaner

The Auto Cleaner will run the Server Cleaner after a certain amount of time has passed. This can be configured to be clean every 5 to 30 minutes.

### Memory Cleaner (experimental, prone to crashing)

The Memory Cleaner tries to despawn anything that isn't being rendered, meaning it will look for any lines of code that belong to a spawnable that isn't currently being used (e.g. a spawnable that hasn't been despawned properly) and attempt to remove it.

The Memory Cleaner is a very advanced cleaner, though, and should only be used **very rarely**, or if the server has been on for an extended period of time and **the Server Cleaner isn't helping with the lag anymore** as it has a high chance of crashing.

## Installation

Installing FsO is as simple as installing a mod you've installed before. If you're new to modding BONELAB, then follow these instructions. Otherwise, you can ignore them.

### Automatic

Use a Thunderstore Mod Manager (e.g. [Thunderstore Mod Manager](https://www.overwolf.com/app/thunderstore-thunderstore_mod_manager), [r2modman](https://github.com/ebkr/r2modmanPlus), [Gale](https://github.com/Kesomannen/gale), etc.) to install the mod. This should also install FsO's dependencies alongside FsO, too.

Note: if you want to use FsO (or any other mod) with BONELAB, you need to open the game in the mod manager you chose to install FsO with, as the mods that are installed on Thunderstore Mod Managers are kept in a separate folder, away from BONELAB's files.

### Manual

These instructions are for Steam installations, but Meta PC App installations shouldn't be too far off.

1. Install [MelonLoader](https://melonwiki.xyz), [BoneLib](https://thunderstore.io/c/bonelab/p/gnonme/BoneLib/) and [Fusion](https://thunderstore.io/c/bonelab/p/Lakatrazz/Fusion/) if you haven't already.
2. Download this mod via the "Manual Download" button.
3. Extract the contents of the ZIP folder that was just downloaded (named `Popper-FsOptimizer-X.X.X.zip`)
4. Open your Steam Library, right-click on BONELAB, hover over `Manage` and click `Browse Local Files`. This should open another window/tab that shows BONELAB's installation.
5. Copy the `FsOptimizer.dll` file from the extracted ZIP folder from step 3 and paste it into the `Mods` folder in BONELAB's installation.
6. Open the game via Steam, and you should be able to access the mod through the BoneMenu in-game!

## Troubleshooting

If an SDK mod (a mod installed from mod.io) has stopped it's functionality after the Memory Cleaner was used, try spawning it again. If that doesn't work, or if a code mod (a mod installed from Thunderstore) stops working, make sure you report it in my Discord server, then try using the Level Reloader or restart the game to try and fix it.

FsO has only been tested on PCVR, so if you're using this on Quest (or any other platform for that matter), YMMV. If you do face any issues though, make sure you let me know!

If you have any other issues, feedback or criticisms, feel free to contact me on my Discord server: <https://discord.gg/aQhhXRDqn9>

## Credits

- Popper - Creator & Lead Dev  
  [YouTube](https://www.youtube.com/@PopperVids) | [GitHub](https://github.com/PopperVids) | [Discord](https://discord.com/users/775549612135940136)
- Kine - Creator of Documentation & Mod Icon  
  [YouTube](https://www.youtube.com/@FineMineKine) | [GitHub](https://github.com/FineMineKine) | [Discord](https://discord.com/users/666869061623349250)

### Minor Attributions

- The [BONELAB Mod Icon Creator Figma Template](https://www.figma.com/community/file/1218386424917309834) created by [@aiden on Figma](https://www.figma.com/@aiden_) was used to make the logo.
