# BotsAI

AI bots for ROUNDS that work **online** - in RoundsWithFriends rooms with your friends - as well as in the LOCAL lobby.
Author: Shidzenreku13. Source: https://github.com/Shidzen13/BotsAI
Made with heavy AI assistance: I design and play-test it, an AI writes most of the code.

![Online lobby with bots](https://raw.githubusercontent.com/Shidzen13/BotsAI/main/docs/images/lobby.png)

![Bots in a match](https://raw.githubusercontent.com/Shidzen13/BotsAI/main/docs/images/battle.png)

## Controls
- **LOCAL lobby:** `B` - add a bot.
- **RoundsWithFriends online room:**
  - `B` - add a bot (it readies itself), `N` - remove the last bot;
  - `1`-`4` - next color (team) for bot #N, `Shift` + `1`-`4` - previous color;
  - hints and the bot list are shown at the bottom-left of the lobby.
- Colors are teams only in Team Deathmatch.

## Online
- Bots count as players, so you can start an online room alone with bots.
- Default: 2 characters per PC (you + 1 bot), same as RWF - safe with friends without this mod.
- Up to 4 bots: set "Online: characters per PC" to 5 in the in-game menu (or `[Online] MaxCharactersPerClient = 5` in `BepInEx/config/com.kanty.rounds.bots.cfg`).
  Use it alone or when every player has BotsAI with the same value.
- A bot thinks on the PC of the player who added it.

## Settings
In-game: **Options -> Mod options -> BotsAI** (difficulty, card pick delay, stalemate timer, characters per PC, auto rematch).
Same values live in `BepInEx/config/com.kanty.rounds.bots.cfg`.

## Requirements
- BepInEx, UnboundLib (installed automatically).
- Online bots need RoundsWithFriends (optional; LOCAL lobby works without it).

## Compatibility
- Tested with a ~30-mod pack (card packs, extra maps, RoundsWithFriends, Toggle Friendly Fire).
- Built-in fixes for mods that treat "the local player" as any character on your PC (your bots included):
  - **RoundsWithFriends** - bots join the online lobby as players, show as "Bot N" with the vanilla face;
  - **Hidden Cards** - you still see your own card choice and card bar, not a bot's;
  - **Infoholic** - the stats panel shows your stats, not a bot's;
  - **Toggle Friendly Fire** - bots read the self-damage setting (e.g. Demonic Pact is only valued when self-damage is off).
- Bots know vanilla cards well. Modded cards are recognised as modded and picked a bit more carefully,
  but bots do not understand their special effects - picks can be odd.
- Card mods can hide a card from bots with the `NotForBots` card category (same convention as RoundsWithBots).
- Maps with moving or breakable parts are rescanned every 1-2 s; still, unusual modded maps (hazards, closed rooms) can make bots get stuck or play passively.
  A safety rule ends a point after 60 s without damage when only your bots are alive.
- Mods that heavily change movement, blocking or game modes may break bot behaviour.
- Found a problem? Note the mod list and attach `BepInEx/LogOutput.log`.

## Other
- If only your bots are alive and nobody takes damage for 60 s, the weakest bot dies and the point ends
  (`[Bots] ForceEndPointSeconds`, 0 = off).
- Bots know vanilla vs modded cards and respect Toggle Friendly Fire (no self-damage).
- Aiming accounts for gravity, bullet drag and bullet speed changes, and avoids walls on the bullet path.

---

# BotsAI (UA)

Боти для ROUNDS, що працюють **онлайн** - у кімнатах RoundsWithFriends разом з друзями - і в LOCAL лобі. Автор: Shidzenreku13. Зроблено з активною допомогою ШІ.
- Налаштування в грі: Options -> Mod options -> BotsAI.
- LOCAL лобі: `B` - додати бота.
- Онлайн-кімната RWF: `B` - додати, `N` - прибрати, `1`-`4` - колір бота №N вперед, `Shift`+`1`-`4` - назад.
- Вбудована сумісність: RoundsWithFriends (боти як гравці «Bot N»), Hidden Cards (бачиш свої картки), Infoholic (стата — твоя), Toggle Friendly Fire (боти враховують самошкоду).
- Сумісний з більшістю модів карток і мап; модові картки й незвичні мапи боти розуміють гірше — можливі дивні вибори чи застрягання.
- До 4 ботів: `[Online] MaxCharactersPerClient = 5` (лише самому або коли в усіх мод з тим самим значенням).
