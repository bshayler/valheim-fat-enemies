# Gloom Companion

An original, content-free companion tracker for cooperative dungeon-crawl board games, in the spirit of the (now discontinued) Gloomhaven Helper. It tracks:

- round flow: set initiative → **Draw** → turns sorted by initiative → **Next round**
- characters: HP, XP, loot, conditions, exhausted
- monster types: normal/elite stats, standees with HP and conditions, ability-card deck (draw/discard per round)
- element board (strong → waning → inert, auto-decay each round)
- monster attack-modifier deck (draw, shuffle-at-end-of-round flag)
- 50-step undo/redo, autosave, JSON import/export

**No copyrighted art or card text is included.** You enter your own monsters and ability cards (or import your own JSON). The default modifier deck is generic.

## Run on a Mac

Quickest: open `index.html` in Safari/Chrome — nothing to install.

As a standalone app (needs Node.js):

```bash
cd gloom-companion
npm install
npm start            # run
npm run build:mac    # produce a .dmg in dist/
```

## Import format

```json
{ "groups": [ { "name": "Cave Goblin", "max": 10, "flying": false,
    "stats": { "normal": {"hp":6,"move":2,"attack":2,"range":0},
               "elite":  {"hp":10,"move":2,"attack":3,"range":0} },
    "deck": [ {"init":20,"title":"Quick Strike","text":"Move 1, Attack 2"} ] } ],
  "chars": [ {"name":"Aria","cls":"Scout","level":1,"maxHp":10} ] }
```
