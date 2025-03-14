# Welcome to op.io

# Local setup instructions

* Clone the repo to your client using Git, or download the zip file and extract it to your desired location
* From Windows, download sqlite-tools-win-x64-3490100.zip in the following link: https://www.sqlite.org/download.html
* Move sqlite to your C drive
* Add the sqlite folder to your system environmental variables PATH
* To initialize an uninitialized database, do:
  > sqlite3 C:\...\Data\GameData.db
  > .read C:\...\Data\InitDatabase.sql
* Download sqlitebrowser from https://sqlitebrowser.org/dl/ to visualize the tables
* Lauanch DB Browser for SQLite then `Open Database` and select Data/GameData.db

## TODO (migrate):

* https://zapier.com/editor/282794365/published

* Write a .bat script to reinit database
* Add enable/disable player/destructable/collidable, and also have an opacity option
* Split JSON files and correct parsisng
* JSON to SQLite migration
* --------------
* Add more sophisticated Debugging methodology
* --------------
* Migrate inputs to InputManager.cs and a new input json file
* Write case tests for all functions
* Ensure redundant code is simplified away, e.g., ensure Player.cs and StaticObject.cs are not any longer than absolutely necessary and modularize as needed, also ensure Physics are split up
* Create Agent class for GOs that can move and interact with the environment
* Ensure Player movement uses Physics script
* Refactor physics functions, e.g., velocity/accel, collisions, etc
* Add leveling up from destroying GameObjects
* Add regeneration of farm
* Fix physics having no acceleration
* Add stochastic farm starting rotation
* Add farm hover effect when sitting still
* Hotline miami shift camera movement
* Add collision and physics options to shapes
* Add rotations for all shapes
* Compartmentalize different physics functions
* Add outlines to the shapes, soften their edges
* Add cards I can drag on the side of the screen
* Make each card procedurally generated and an NFT you can trade with other players
* Make the screen resizable: square in middle for playing, cards and misc settings in remaining space
* Fix collision physics so when destroy is false, it pushes shapes based on weight
* Add health to objects
* Use GameObject more for handling physics, ensure movement updates GameObject position
* Add rotation to player with shooter stick to see which direction you are looking
* Add stochastic floating movement to farm
* Add dice rolling mechanic
* Factory rolls out loot
* 80's graphics theme
* Customizable off-screen info graphics
* Crack open loot chests once you get home, it's a satisfying visual process
* Large enemies have parasites inside them, which gives you your upgrade
* Ecosystems?
* Base is a cave
* Maybe each time you die, it makkeks a trophy, like porco marco plane grave
* Antifascist themes
* You acquire new bases over time which you can revisit.  People with quests will visit your settlements and you have to periodically defend them.  Maybe settlements are like ships, so you leave it and return to it before/after the mission, and each settlement you capture you can extract resources from.  Or you can destroy a settlement for a tarot card or decide to keep it.

## Finished:
* Add farm, shapes, polygons, player, player console-agnostic movement, collision
* Fix non circle shape edge artifacting
* Make static shapes
