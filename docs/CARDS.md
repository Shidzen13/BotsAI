# ROUNDS — база карток для бота

Згенеровано автоматично (скрипти в scratchpad: `extract2.py`, `effects.py`, `build.py`, `gen_docs.py`). Машинна версія: `docs/cards.json`.

## Підсумок

| Мод | Активних карток | Звідки значення |
|---|---|---|
| Vanilla | 67 | Значення витягнуто з ассетів гри (UnityPy + генерація typetree з Managed DLL): `sharedassets0.assets`, `resources.assets`, пул — `CardChoice.cards` у `level0`. Радіуси/шкода ефектів — з префабів ефектів (Explosion, Saw тощо). |
| RSCards (RS_Mind) | 15 | Мод RS_Mind-RSCards 1.7.5. Картки — Unity-префаби у вбудованому AssetBundle `rscardart`; значення витягнуто з бандла (UnityPy), список активних — з `CardHolder`. |
| KeysCards (Keys) | 70 | Мод Keys-KeysCards. Картки в коді (`CustomCard.BuildCard`, 70 шт.); числа — з `SetupCard` (декомпіляція ilspycmd), умовні бонуси — з `*Mono`-класів. |
| **Разом** | **152** | |

Не в пулі (не рахуються): 15 — див. кінець файлу. Інші встановлені моди карток не додають (UnboundLib/ModdingUtils/RarityLib/CardChoiceSpawnUniqueCardPatch — лише інфраструктура; WeaponsManager — для Spare Shotgun). Hidden Cards (AncientKoala) карток не додає — лише ховає карти суперників у барі.

Рідкість: vanilla 0=Common, 1=Uncommon, 2=Rare. KeysCards додає рідкості `Hidden` (вага 0.25, комбо-картки) і `Treasure` (вага 0.105, скарби) через RarityLib.

## Як читати `effects`

- `name` = точний `CardInfo.cardName` у грі (з оригінальним регістром і помилками, напр. `PRISITNE PERSEVERENCE`, `Target BOUNCE`), а не ім'я GameObject.
- Це лише те, що реально копіює `ApplyCardStats` при виборі (для модових — значення, які `SetupCard` записує в Gun/CharacterStatModifiers/Block картки). Текст картки інколи бреше — розбіжності в `notes`.
- `*Mult` — множник (1 = без змін). **`attackCooldownMult`, `reloadTimeMult`, `blockCooldownMult`: менше 1 = КРАЩЕ** (це множники часу). `attackCooldownMult` 0.5 = стріляє вдвічі частіше.
- `*Add` / `*AddSec` — додавання (`ammoAdd` набої, `reloadTimeAddSec` секунди перезарядки, `blockCooldownAddSec` секунди кулдауну блоку, `bouncesAdd` відскоки, `projectilesAdd` кулі за постріл, `burstsAdd` черга).
- `bulletSpeedMult` — швидкість кулі (змінює траєкторію: швидша = пряміша). `bulletSimSpeedMult` — швидкість симуляції (та сама траєкторія, але швидше/повільніше в часі). `bulletGravityMult` — гравітація кулі.
- `bulletLifetimeSec` = `destroyBulletAfter` (обмежує дальність). `lifeStealAdd` 1.0 = 100% вампіризму. `regenPerSec` — HP/с.
- `effect*`, `aura*`, `*Radius` — з префабів ефектів (радіус в одиницях світу; арена ±35.6 × ±20). `cond_*`/`onBlock_*`/`perBlockStack_*` — умовні бонуси, НЕ постійні стати. `critChanceAddPct` — шанс криту KeysCards (крит = x2 шкоди).
- Довідка: база — 3 набої, перезарядка ~1.5 с, куля |v|≈50×bulletSpeed, g≈100×bulletGravity. Бот тримає дистанцію 13–16.
- `closeRange: true` — основна сила картки працює лише ближче ~10 (радіус ефекту ≤ 10 або коротка дальність кулі), тобто НЕ на дистанції бота 13–16.
- `botRisk`: none / low / medium / high — наскільки картка шкідлива чи марна для поточного бота.

## 10 порад для вибору карток

1. **Універсальні «чисті плюси» — пріоритет.** Quick Reload (x0.3 перезарядки), Fast forward, Steady shot, HUGE, Tank, Decay, Phoenix, Burst, Reckless Attack, Nimble/Nimble+, Iron Pincers, Saw Shot — не мають умов і працюють на будь-якій дистанції.
2. **Дальній бій (стиль бота 13–16):** швидкість кулі (Quick Shot, Fast ball, Steady shot, Hitscan, Fast forward) спрощує упередження; Grow росте з дистанцією; Homing і Target BOUNCE компенсують промахи; Cold bullets/Dazzle сповільнюють/станять ціль. Scaredy Cat дає x1.5 шкоди саме коли ворог далі 12.
3. **Ближній бій:** on-block AoE (Shockwave r6.5, Saw r4.5, Overpower r4, Silence r5, Frost slam r5, Static field r3, Harming Field r2, Implode r9), аури (Chilling Presence 4.5, Lifestealer 4, Ecliptic Library 7, Bewitching 6, Twin Scythe 2), шотган Buckshot (~9). Брати лише якщо бот перемикається на агресивну дистанцію ≤5.
4. **Empower — міст між стилями:** після блоку постріл x1.5 і активує всі on-block ефекти там, де влучить. З ним Shockwave/Saw/Overpower/Silence/Static field стають дальніми. Бот має стріляти одразу після блоку.
5. **Синергії блоку:** Echo (+1 блок), Iron Carapace/Grovetenders Shield (+блоки), Defender/Mirror (x0.7–0.5 кулдауну) множать будь-які on-block ефекти й зілля KeysCards. Tactical reload і Refresh роблять блок джерелом перезарядки/нового блоку.
6. **HP-синергії:** Overpower б'є на 15% макс. HP, Pristine perseverence (x5 HP вище 90%) + регенерація (Regenerator +10/с, Iron Heart +15/с, Giants Gauntlet 2.5%/с), Brawler (x3 HP 3 с після шкоди). Mending Reload/Druids Blessing/Healing field — лікування.
7. **Відскоки:** Bouncy/Riccochet/Rebound/Trickster/Reflection Accelerant/Mayhem корисні лише якщо прицілювання бота враховує відбиття; Trickster і Reflection Accelerant посилюються з кожним відскоком. Bounce Absorption (RS) з'являється лише при ≥2 відскоках і конвертує їх у x2 шкоди — добре для бота з прямим прицілом.
8. **Багато куль / швидкострільність:** Barrage, Buckshot, SPRAY, Magic Trick, Merlins Staff множать on-hit ефекти (Poison, Explosive, Spellbound, Cold, Toxic cloud). Ці картки ріжуть шкоду за кулю — уникати з Snipers Nest (ставить магазин 1).
9. **Читати реальні числа, а не текст:** Iron Carapace і Overdrive не зменшують кулдаун блоку (пишуть у `block.cooldown`, якого `ApplyCardStats` не копіює); Treasure Map/Compass не дають швидкості атаки; Hitscan має прихований +1 с перезарядки; Toxic cloud ріже шкоду x0.75; Glass cannon — HP x0.5.
10. **Ланцюжки KeysCards варто доводити до кінця:** Metamorphosis → Iron-картки (дуже сильні Common; Iron-картка забирає Metamorphosis); Treasure Map + Compass, Cursed Plunder, Treasure Trove, Expedition, Golden Egg → скарби (найсильніші картки); 2 зілля → Alchemist; Automatic-картка (Saw Shot, Slow Start, Druids, Dead Mans, Bewitching) відкриває Faster Hardware/Overdrive/Nimble+/Nuclear Reactor.

## Погані / ризиковані для бота

| Картка | Мод | Ризик | Чому |
|---|---|---|---|
| ABYSSAL COUNTDOWN | Vanilla | high | Бот постійно рухається, тому майже ніколи не активує. Погано для бота. |
| Demonic pact | Vanilla | high | dontAllowAutoFire: треба окремо клацати на кожну кулю. Бот, що затискає постріл, стрілятиме лише раз/спамитиме і вбиватиме себе. Ризиковано. |
| REMOTE | Vanilla | high | Бот не керує кулею після пострілу — фактично лише мінус до швидкості. Погано для бота. |
| SHIELD CHARGE | Vanilla | high | Ривок у напрямку руху може скинути бота з платформи чи в пилку; бот блокує для захисту, а не для атаки. |
| Sentry | KeysCards | high | Бот не присідає під час блоку, тож отримує лише -25% кулдауну. Якщо випадково увімкне — стоятиме майже нерухомо. |
| Spare Shotgun | RSCards | high | Бот не перемикає зброю — картка марна для бота, доки бот не навчиться Q/E. |
| TELEPORT | Vanilla | high | Небезпечно для бота: телепорт у прірву/пилку/за межу арени під час захисного блоку. |
| Vampiric Block | KeysCards | high | Бот, що часто блокує, вбиватиме себе. Брати тільки якщо бот обмежує частоту блоку. |
| All or Nothing | KeysCards | medium | x1.55 шкода (текст +65%), перезарядка x0.67, HP x0.75 (текст -30%), +0.5 с кулдауну блоку. |
| BUCKSHOT | Vanilla | medium | Дальність ~8-10 одиниць через drag+lifetime. Бот на 13-16 не влучить — або підходити впритул, або не брати. Шкода x0.4 за кулю (5 куль разом ~x2 впритул). |
| GLASS CANNON | Vanilla | medium | Для бота, що погано ухиляється, -50% HP дуже боляче. |
| Implode | Vanilla | medium | Затягує суперника впритул — добре лише для ближньої збірки. |
| MAYHEM | Vanilla | medium | Кулі довго літають по арені й можуть влучити в самого бота. |
| Moonwalker | KeysCards | medium | Низька гравітація ламає навігацію/стрибки бота (A* розрахований на звичайну гравітацію). |
| Mortar | RSCards | medium | Рідкість у бандлі = Rare (README: Uncommon). Взаємовиключна з Hitscan. Поведінка залежить від того, куди бот цілиться в момент наступного пострілу. |
| Raid Boss | KeysCards | medium | Взаємовиключна з Pipsqueak. Великий і повільний — легка ціль, але дуже живучий. |
| SHIELDS UP | Vanilla | medium | +0.5 с перезарядки і +0.5 с кулдауну блоку. |
| Zephyrs Blessing | KeysCards | medium | Скарб. Текст '+20% Damage' у коді не встановлено. Низька гравітація заважає навігації бота. |

## Vanilla — 67 карток

Значення витягнуто з ассетів гри (UnityPy + генерація typetree з Managed DLL): `sharedassets0.assets`, `resources.assets`, пул — `CardChoice.cards` у `level0`. Радіуси/шкода ефектів — з префабів ефектів (Explosion, Saw тощо).

### ABYSSAL COUNTDOWN
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** high
- **Теги:** close-range, defensive, aura, conditional
- **Числа:** `standStillSec`=7, `buffDurationSec`=5, `buffHealthMult`=2.0, `sawDamage`=90, `sawRadius`=4.5
- **Механіка:** Стояти на місці ~7 с (timeToFill 7) -> на 5 с: HP x2 і пилка навколо (Saw: 90 шкоди, радіус 4.5). Заряд спадає за 10 с руху.
- **Примітки:** Бот постійно рухається, тому майже ніколи не активує. Погано для бота.

### BARRAGE
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, multi-projectile
- **Числа:** `damageMult`=0.3, `reloadTimeAddSec`=0.25, `ammoAdd`=5, `projectilesAdd`=4, `spreadAdd`=0.13
- **Механіка:** Шотган-залп: +4 кулі одночасно з розкидом.
- **Примітки:** На першій такій картці шкода x0.3 діє повністю; якщо вже є кілька куль, множник шкоди пом'якшується пропорційно (CopyGunStats). Добре з on-hit ефектами (poison, explosive).

### BIG BULLET
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `reloadTimeAddSec`=0.25, `bulletSizeAdd`=0.5
- **Механіка:** Більші кулі (size +0.5) — легше влучити.
- **Примітки:** Слабка, але безпечна.

### BOMBS AWAY
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, defensive, on-block, explosive
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectDamage`=30, `effectRadius`=4
- **Механіка:** Блок випускає кільце маленьких бомб навколо (вибух 30 шкоди, радіус 4).
- **Примітки:** Бомби летять повільно (projectileSpeed 0.3) — корисно лише впритул.

### BOUNCY
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, bounce
- **Числа:** `damageMult`=1.25, `reloadTimeAddSec`=0.25, `bouncesAdd`=2
- **Механіка:** +2 відскоки від стін.
- **Примітки:** Відскоки допомагають влучати з-за укриття; дальність не обмежена.

### BRAWLER
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** low
- **Теги:** defensive, conditional
- **Числа:** `buffHealthMult`=3.0, `buffDurationSec`=3
- **Механіка:** HP x3 на 3 с після того, як завдав шкоди.
- **Примітки:** Працює на будь-якій дистанції, але найкраще в постійному обміні шкодою.

### BUCKSHOT
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** medium
- **Теги:** close-range, multi-projectile
- **Числа:** `damageMult`=0.4, `reloadTimeAddSec`=0.25, `ammoAdd`=5, `projectilesAdd`=4, `bulletSpeedMult`=3.5, `bulletGravityMult`=0.0, `spreadAdd`=0.5, `knockbackMult`=2.5, `bulletLifetimeSec`=0.2, `bulletDragAdd`=10.0, `approxRange`=9
- **Механіка:** Шотган: +4 кулі, сильний розкид 0.5, drag 10, кулі зникають через 0.2 с, без гравітації.
- **Примітки:** Дальність ~8-10 одиниць через drag+lifetime. Бот на 13-16 не влучить — або підходити впритул, або не брати. Шкода x0.4 за кулю (5 куль разом ~x2 впритул).

### BURST
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, multi-projectile
- **Числа:** `damageMult`=0.6, `reloadTimeAddSec`=0.25, `ammoAdd`=3, `burstsAdd`=3, `timeBetweenBulletsAdd`=0.025, `spreadAdd`=0.04
- **Механіка:** Постріл чергою з 3 куль (bursts 3, 0.025 с між кулями).
- **Примітки:** Хороша універсальна; шкода x0.6 на кулю = x1.8 за постріл.

### CAREFUL PLANNING
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `damageMult`=2.0, `attackCooldownMult`=2.5, `reloadTimeAddSec`=0.5
- **Механіка:** x2 шкода, але постріл утричі рідше (cooldown x2.5).
- **Примітки:** Добре для снайпера з точним прицілом.

### CHASE
- **Рідкість:** Uncommon · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** mobility, close-range
- **Числа:** `healthMult`=1.3, `conditionalMoveSpeedMult`=1.6
- **Механіка:** +60% швидкості руху, коли рухаєшся до суперника.
- **Примітки:** Бот, що тримає дистанцію, майже не отримує бонус; HP +30% корисне завжди.

### CHILLING PRESENCE
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, aura, defensive
- **Числа:** `healthMult`=1.25, `auraRadius`=4.5, `auraSlow`=0.5
- **Механіка:** Аура: сповільнює ворогів у радіусі 4.5 (slow 0.5, макс 0.7).
- **Примітки:** На 13-16 не дістає, але +25% HP — нормально.

### COLD BULLETS
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, on-hit
- **Числа:** `reloadTimeAddSec`=0.25, `bulletSlowAdd`=0.7
- **Механіка:** Кулі сповільнюють ціль (+0.7 slow).
- **Примітки:** Сповільнена ціль = легше влучати з дальньої дистанції. Добре для бота.

### COMBINE
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range
- **Числа:** `damageMult`=2.0, `reloadTimeAddSec`=0.5, `ammoAdd`=-2
- **Механіка:** x2 шкода, -2 набої, +0.5 с перезарядки.
- **Примітки:** При базових 3 набоях лишається 1 — довгі перезарядки. Добре з Quick Reload.

### DAZZLE
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, on-hit
- **Числа:** `reloadTimeAddSec`=0.25
- **Механіка:** Влучання кілька разів стане ціль (stun 0.1 с кожні 0.35 с).
- **Примітки:** Безпечний контроль.

### DECAY
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `healthMult`=1.5, `damageTakenOverSec`=4.0
- **Механіка:** Отримана шкода розтягується на 4 с; +50% HP.
- **Примітки:** Чудова захисна, дає час на регенерацію/лікування.

### DEFENDER
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `healthMult`=1.3, `blockCooldownMult`=0.7
- **Механіка:** Кулдаун блоку x0.7, +30% HP.
- **Примітки:** Добре для бота, що вміє блокувати кулі.

### Demonic pact
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** high
- **Теги:** utility, self-damage
- **Числа:** `attackCooldownMult`=0.025, `reloadTimeAddSec`=0.25, `ammoAdd`=9, `noAutoFire`=1, `selfDamagePerShot`=5, `effectDamage`=2, `effectRadius`=4
- **Механіка:** Без кулдауну пострілу (attackSpeed x0.025), +9 набоїв, кожна куля дає вибух 2 шкоди r4; кожен постріл коштує HP (DealDamageToPlayer 5, текст: 10).
- **Примітки:** dontAllowAutoFire: треба окремо клацати на кожну кулю. Бот, що затискає постріл, стрілятиме лише раз/спамитиме і вбиватиме себе. Ризиковано.

### DRILL AMMO
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, utility
- **Числа:** `reloadTimeAddSec`=0.25
- **Механіка:** Кулі проходять крізь стіни до 7 м.
- **Примітки:** Корисно для бота, що стріляє з-за укриття; але бот має знати, що може стріляти крізь стіни.

### ECHO
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, on-block
- **Числа:** `healthMult`=1.3, `extraBlocks`=1
- **Механіка:** +1 додатковий (затриманий) блок; +30% HP.
- **Примітки:** Подвоює всі on-block ефекти.

### EMP
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block, defensive
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectSlow`=0.7
- **Механіка:** Блок випускає кільце з 30 сповільнюючих куль (slow 0.7, шкода x0.1, drag 10).
- **Примітки:** Через drag 10 кулі швидко гальмують — дія лише поруч; +30% HP.

### EMPOWER
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, on-block, utility
- **Числа:** `blockCooldownAddSec`=0.25, `nextShotDamageMult`=1.5, `nextShotSpeedMult`=1.5
- **Механіка:** Після блоку наступний постріл x1.5 шкода і x1.5 швидкість; куля активує всі on-block ефекти там, де влучить.
- **Примітки:** Ключова синергія: перетворює ближні on-block ефекти (Shockwave, Static field, Saw, Overpower...) на дальні. Боту треба стріляти одразу після блоку.

### EXPLOSIVE BULLET
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, explosive, on-hit
- **Числа:** `attackCooldownMult`=2.0, `reloadTimeAddSec`=0.25, `effectDamage`=30, `effectRadius`=4
- **Механіка:** Куля вибухає при влучанні: 30 шкоди, радіус 4, сильна відкидка. Постріл удвічі рідше.
- **Примітки:** Вибух б'є і по собі, якщо стріляти впритул. На дистанції — сильна.

### Fast forward
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `reloadTimeMult`=0.7, `bulletSimSpeedMult`=2.0
- **Механіка:** Симуляція кулі x2 (летить удвічі швидше по тій самій траєкторії), перезарядка x0.7.
- **Примітки:** Дуже добра для бота: траєкторія не змінюється, лише час польоту.

### FASTBALL
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `attackCooldownMult`=1.5, `reloadTimeAddSec`=0.25, `bulletSpeedMult`=3.5
- **Механіка:** x3.5 швидкість кулі, постріл у 1.5 рази рідше.
- **Примітки:** Швидка куля = менше упередження, краща точність на дальній дистанції.

### FROST SLAM
- **Рідкість:** Common · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block, defensive
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectRadius`=5, `effectSlow`=2.2
- **Механіка:** Блок сповільнює ворогів навколо (slow 2.2, радіус 5); +30% HP.

### GLASS CANNON
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** long-range, risky
- **Числа:** `damageMult`=2.0, `reloadTimeAddSec`=0.25, `healthMult`=0.5
- **Механіка:** x2 шкода, HP x0.5, +0.25 с перезарядки.
- **Примітки:** Для бота, що погано ухиляється, -50% HP дуже боляче.

### GROW
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, on-hit
- **Числа:** `reloadTimeAddSec`=0.25
- **Механіка:** Кулі набирають шкоду під час польоту.
- **Примітки:** Чим далі ціль, тим сильніше — ідеально для дальнього бою.

### Healing field
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, on-block
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectHeal`=55, `effectRadius`=2, `effectDelaySec`=1.5
- **Механіка:** Блок створює поле, яке через 1.5 с лікує на 55 HP у радіусі 2 (на місці блоку); +30% HP.
- **Примітки:** Щоб вилікуватися, треба лишатися на місці ~1.5 с.

### HOMING
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, homing
- **Числа:** `damageMult`=0.75, `attackCooldownMult`=1.5, `bulletSpeedMult`=0.7, `bulletGravityMult`=0.5
- **Механіка:** Кулі наводяться на видимі цілі (Homing 350). Шкода x0.75, постріл у 1.5 рази рідше, швидкість x0.7, гравітація x0.5.
- **Примітки:** Компенсує неточний приціл бота. Текст картки каже +0.25 с перезарядки, але в префабі reloadTimeAdd = 0.

### HUGE
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `healthMult`=1.8
- **Механіка:** +80% HP.
- **Примітки:** Простий і сильний вибір.

### Implode
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** medium
- **Теги:** close-range, on-block
- **Числа:** `healthMult`=1.5, `blockCooldownAddSec`=0.25, `effectRadius`=9
- **Механіка:** Блок притягує ворогів у радіусі 9 до себе; +50% HP.
- **Примітки:** Затягує суперника впритул — добре лише для ближньої збірки.

### LEECH
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, lifesteal
- **Числа:** `healthMult`=1.3, `lifeStealAdd`=0.75
- **Механіка:** +75% вампіризм, +30% HP.

### LIFESTEALER
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, aura, lifesteal
- **Числа:** `healthMult`=1.25, `auraRadius`=4, `auraDamage`=5, `auraTickSec`=0.3
- **Механіка:** Аура r4: кожні 0.3 с краде 5 HP у ворога поруч; +25% HP.
- **Примітки:** На дистанції 13-16 не працює.

### MAYHEM
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** long-range, bounce
- **Числа:** `damageMult`=0.85, `reloadTimeAddSec`=0.5, `bouncesAdd`=5
- **Механіка:** +5 відскоків, шкода x0.85, +0.5 с перезарядки.
- **Примітки:** Кулі довго літають по арені й можуть влучити в самого бота.

### OVERPOWER
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectRadius`=4, `effectDamagePctMaxHp`=15
- **Механіка:** Блок: через 0.5 с вибух r4 на 15% твого макс. HP; +30% HP.
- **Примітки:** Синергія з HP-картками (Tank, HUGE).

### PARASITE
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, dot, lifesteal
- **Числа:** `damageMult`=1.25, `reloadTimeAddSec`=0.25, `healthMult`=1.25, `lifeStealAdd`=0.5
- **Механіка:** Шкода кулі розтягується на 5 с; +50% вампіризм, +25% HP, x1.25 шкода.

### PHOENIX
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `healthMult`=0.65, `respawnsAdd`=1
- **Механіка:** Одне відродження за раунд; HP x0.65.
- **Примітки:** Дуже сильна для бота.

### POISON
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, dot
- **Числа:** `damageMult`=1.7, `reloadTimeMult`=0.7, `ammoAdd`=-1
- **Механіка:** x1.7 шкода, розтягнута на 3 с; перезарядка x0.7; -1 набій.

### PRISITNE PERSEVERENCE
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, conditional
- **Числа:** `conditionalHealthMult`=5.0, `hpThreshold`=0.9
- **Механіка:** HP x5, поки здоров'я вище 90%.
- **Примітки:** Синергія з регенерацією/лікуванням.

### QUICK RELOAD
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `reloadTimeMult`=0.3
- **Механіка:** Перезарядка x0.3.
- **Примітки:** Одна з найкращих загальних карток.

### QUICK SHOT
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `reloadTimeAddSec`=0.25, `bulletSpeedMult`=2.5
- **Механіка:** x2.5 швидкість кулі.
- **Примітки:** Пряміша траєкторія, легше прицілитися.

### RADAR SHOT
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, on-block
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectRadius`=12
- **Механіка:** Блок сканує радіус 12 і автоматично стріляє по знайдених ворогах; +30% HP.
- **Примітки:** Радіус 12 — працює з середньої дистанції.

### RADIANCE
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** `healthMult`=1.3, `effectDamage`=4
- **Механіка:** Під час перезарядки випускає сонячні хвилі (4 шкоди), частота росте; +30% HP.
- **Примітки:** Радіус хвиль у даних не знайдено (LineRangeEffect: 4 шкоди, відкидка 100, живе 0.7 с) — ймовірно ближній.

### Refresh
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, on-hit
- **Числа:** `damageNeeded`=10
- **Механіка:** Відновлює блок після 10 нанесеної шкоди.
- **Примітки:** Сильно для бота, що часто влучає.

### REMOTE
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** high
- **Теги:** utility, needs-input
- **Числа:** `reloadTimeAddSec`=0.25, `bulletSpeedMult`=0.6
- **Механіка:** Кулями керують правим стиком/мишею. Швидкість кулі x0.6.
- **Примітки:** Бот не керує кулею після пострілу — фактично лише мінус до швидкості. Погано для бота.

### RICCOCHET
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, bounce
- **Числа:** `attackCooldownMult`=0.75, `reloadTimeAddSec`=0.25, `bouncesAdd`=2, `speedMultPerBounce`=0.5
- **Механіка:** +2 відскоки (втрачають 50% швидкості), постріл x0.75 (швидше).

### SAW
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block, defensive
- **Числа:** `healthMult`=1.3, `blockCooldownAddSec`=0.25, `effectDamage`=60, `effectRadius`=4.5, `effectDurationSec`=2
- **Механіка:** Блок на 2 с створює пилку навколо (60 шкоди, радіус 4.5); +30% HP.
- **Примітки:** Треба бути впритул.

### SCAVENGER
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, on-hit
- **Числа:** `reloadTimeAddSec`=0.5
- **Механіка:** Нанесення шкоди перезаряджає зброю.
- **Примітки:** Добре для швидкострілу/шотгана.

### SHIELD CHARGE
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** high
- **Теги:** close-range, on-block, mobility
- **Числа:** `blockCooldownAddSec`=0.25, `chargeDamage`=25
- **Механіка:** Блок кидає вперед (ривок 0.2 с), 25 шкоди при зіткненні, потім авто-блок.
- **Примітки:** Ривок у напрямку руху може скинути бота з платформи чи в пилку; бот блокує для захисту, а не для атаки.

### SHIELDS UP
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** defensive, utility
- **Числа:** `reloadTimeAddSec`=0.5, `blockCooldownAddSec`=0.5
- **Механіка:** Останній постріл з магазину автоматично блокує; вимикає безперервну перезарядку.
- **Примітки:** +0.5 с перезарядки і +0.5 с кулдауну блоку.

### Shockwave
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, on-block, defensive
- **Числа:** `healthMult`=1.5, `blockCooldownAddSec`=0.25, `effectRadius`=6.5
- **Механіка:** Блок відштовхує ворогів (сила 30000, радіус 6.5, slow 0.5); +50% HP.
- **Примітки:** Добре проти ближніх суперників; +50% HP завжди корисні.

### SILENCE
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block
- **Числа:** `healthMult`=1.25, `blockCooldownAddSec`=0.25, `effectRadius`=5, `silenceSec`=3.2, `effectDamage`=10
- **Механіка:** Блок: тиша (не можуть стріляти/блокувати) 3.2 с + 10 шкоди в радіусі 5; +25% HP.

### SNEAKY
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, utility
- **Числа:** `reloadTimeAddSec`=0.25
- **Механіка:** Кулі облітають землю (DodgeGround).

### SPRAY
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, multi-projectile
- **Числа:** `damageMult`=0.25, `attackCooldownMult`=0.0909, `reloadTimeAddSec`=0.25, `ammoAdd`=12, `spreadAdd`=0.1, `recoilMult`=0.25
- **Механіка:** Кулемет: постріл x0.09 (у ~11 разів частіше), +12 набоїв, шкода x0.25, розкид.
- **Примітки:** Розкид знижує влучність на дальній дистанції; бот має затискати постріл.

### STATIC FIELD
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block
- **Числа:** `blockCooldownAddSec`=0.25, `effectRadius`=3, `effectSlow`=0.85, `effectDurationSec`=4.5
- **Механіка:** Блок створює поле r3 на ~4.5 с, яке сповільнює (0.85) і завдає шкоди ворогам у ньому.

### STEADY SHOT
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, defensive
- **Числа:** `reloadTimeAddSec`=0.25, `bulletSpeedMult`=2.0, `healthMult`=1.4
- **Механіка:** +40% HP, x2 швидкість кулі.
- **Примітки:** Відмінна для бота.

### SUPERNOVA
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, on-block
- **Числа:** `healthMult`=1.5, `blockCooldownAddSec`=0.5, `pullRadius`=7, `effectRadius`=12, `effectDamage`=30
- **Механіка:** Блок: поле r7 тягне ворогів 2.1 с, потім вибух r12 (30 шкоди, slow); +50% HP.
- **Примітки:** Радіус 12 — дістає і з середньої дистанції.

### TACTICAL RELOAD
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, on-block
- **Числа:** `blockCooldownAddSec`=0.25
- **Механіка:** Блок перезаряджає зброю.
- **Примітки:** Добре для бота, що часто блокує.

### TANK
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `attackCooldownMult`=1.25, `reloadTimeAddSec`=0.5, `healthMult`=2.0
- **Механіка:** HP x2, постріл x1.25 (повільніше), +0.5 с перезарядки.

### Target BOUNCE
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, bounce, homing
- **Числа:** `damageMult`=0.8, `reloadTimeAddSec`=0.25, `bouncesAdd`=1
- **Механіка:** +1 відскок, після відскоку куля летить у видиму ціль; шкода x0.8.
- **Примітки:** Компенсує промахи.

### TASTE OF BLOOD
- **Рідкість:** Uncommon · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility, lifesteal, on-hit
- **Числа:** `lifeStealAdd`=0.3, `buffMoveSpeedMult`=1.5, `buffDurationSec`=3
- **Механіка:** +30% вампіризм; 3 с після шкоди: рух x1.5, стрибок x1.25.

### TELEPORT
- **Рідкість:** Rare · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** high
- **Теги:** mobility, on-block
- **Числа:** `blockCooldownMult`=0.7, `teleportDistance`=6
- **Механіка:** Блок телепортує вперед на 6 одиниць; кулдаун блоку x0.7.
- **Примітки:** Небезпечно для бота: телепорт у прірву/пилку/за межу арени під час захисного блоку.

### THRUSTER
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, on-hit
- **Числа:** `reloadTimeAddSec`=0.25
- **Механіка:** Кулі мають двигуни, що штовхають ціль (сила 15000).
- **Примітки:** Може виштовхнути суперника за межі арени.

### TIMED DETONATION
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, explosive, on-hit
- **Числа:** `damageMult`=0.85, `reloadTimeAddSec`=0.25, `effectDamage`=30, `effectRadius`=4
- **Механіка:** Кулі ставлять бомби, що вибухають через 0.35 с (30 шкоди, радіус 4); шкода x0.85.

### TOXIC CLOUD
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, on-hit, dot
- **Числа:** `damageMult`=0.75, `attackCooldownMult`=1.2, `reloadTimeAddSec`=0.5, `effectRadius`=2.4, `effectDamage`=8, `effectDurationSec`=5.4
- **Механіка:** Кулі створюють отруйну хмару r2.4 на 5.4 с (8 шкоди/тік, slow 0.7). Постріл x1.2, +0.5 с перезарядки.
- **Примітки:** Прихована шкода кулі x0.75 (на картці не показано).

### TRICKSTER
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, bounce
- **Числа:** `damageMult`=0.8, `reloadTimeAddSec`=0.5, `bouncesAdd`=2, `damageMultPerBounce`=1.8
- **Механіка:** +2 відскоки, кожен відскок x1.8 шкоди; базова шкода x0.8.
- **Примітки:** Сильна з відскоками, але треба цілитися через стіни.

### WIND UP
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `damageMult`=1.6, `attackCooldownMult`=2.0, `reloadTimeAddSec`=0.5, `bulletSpeedMult`=2.0
- **Механіка:** x1.6 шкода, x2 швидкість кулі, постріл удвічі рідше, +0.5 с перезарядки.

## RSCards (RS_Mind) — 15 карток

Мод RS_Mind-RSCards 1.7.5. Картки — Unity-префаби у вбудованому AssetBundle `rscardart`; значення витягнуто з бандла (UnityPy), список активних — з `CardHolder`.

### Bounce Absorption
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated
- **Числа:** `damageMult`=2.0, `bulletSpeedMult`=1.5, `bouncesAdd`=-2
- **Механіка:** Перетворює відскоки на силу: x2 шкода, x1.5 швидкість кулі, -2 відскоки.
- **Примітки:** З'являється лише якщо в гравця вже >= 2 відскоки (категорія BounceAbsorption знімається з чорного списку на PlayerPickStart).

### Changeup
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** `attackCooldownMult`=0.7, `bulletSimSpeedMult`=0.7
- **Механіка:** Постріл x0.7 (швидше), симуляція кулі x0.7 (куля летить повільніше по тій самій траєкторії).
- **Примітки:** Повільна куля = більше упередження.

### Harming Field
- **Рідкість:** Common · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, on-block
- **Числа:** `damageMult`=1.3, `blockCooldownAddSec`=0.25, `effectDamage`=55, `effectRadius`=2, `effectDelaySec`=1.5
- **Механіка:** Блок ставить коло, яке через 1.5 с вибухає: 55 шкоди, радіус 2; x1.3 шкода зброї; +0.25 с кулдауну блоку.
- **Примітки:** Аналог Healing field, але б'є ворогів. Ворог має стояти в колі через 1.5 с — тільки впритул (або з Empower).

### Hitscan
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, exclusive
- **Числа:** `attackCooldownMult`=1.25, `reloadTimeAddSec`=1.0, `bulletSimSpeedMult`=100.0, `bulletGravityMult`=0.0
- **Механіка:** Симуляція кулі x100 (майже миттєве влучання), без гравітації, постріл x1.25 (повільніше), +1 с перезарядки.
- **Примітки:** Взаємовиключна з Mortar. Hitscan_Bullet_Mono обнуляє відскоки кулі (RayHitReflect.reflects = 0) — усі картки на відскоки марні. Прихований +1.0 с reloadTimeAdd (на картці не показано). Для бота — чудова: прямий приціл без упередження.

### Mortar
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** utility, needs-input, exclusive
- **Числа:** `damageMult`=1.35, `reloadTimeAddSec`=0.5, `bulletGravityMult`=0.0
- **Механіка:** Кожен новий постріл розвертає вже випущені кулі в поточному напрямку прицілу; x1.35 шкода, без гравітації, +0.5 с перезарядки.
- **Примітки:** Рідкість у бандлі = Rare (README: Uncommon). Взаємовиключна з Hitscan. Поведінка залежить від того, куди бот цілиться в момент наступного пострілу.

### Open Chamber
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `attackCooldownMult`=0.75, `reloadTimeAddSec`=-0.5, `bulletSpeedMult`=0.85
- **Механіка:** -0.5 с перезарядки, постріл x0.75, швидкість кулі x0.85.

### Rebound
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, bounce
- **Числа:** `bouncesAdd`=1
- **Механіка:** +1 відскок.

### Reckless Attack
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `damageMult`=0.85, `attackCooldownMult`=0.5, `reloadTimeAddSec`=-0.5
- **Механіка:** Постріл x0.5 (удвічі швидше), -0.5 с перезарядки, шкода x0.85.
- **Примітки:** Сильна DPS-картка.

### Reflection Accelerant
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range, bounce
- **Числа:** `damageMult`=1.5, `bulletSpeedMult`=1.25, `bouncesAdd`=2, `speedMultPerBounce`=1.35
- **Механіка:** +2 відскоки, кожен відскок x1.35 швидкості; x1.5 шкода, x1.25 швидкість кулі.
- **Примітки:** Рідкість Rare.

### Regenerator
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `regenPerSec`=10.0
- **Механіка:** +10 HP/с регенерації.

### Repentance
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** low
- **Теги:** defensive, gated
- **Числа:** `healthMult`=2.0, `lifeStealAdd`=-0.5
- **Механіка:** HP x2, вампіризм -0.5.
- **Примітки:** З'являється лише при вампіризмі >= 0.5. Поза тижнем 1 квітня (активна версія).

### Slug
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `damageMult`=2.0, `bulletSpeedMult`=0.75
- **Механіка:** x2 шкода, швидкість кулі x0.75.

### Spare Shotgun
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** high
- **Теги:** utility, needs-input
- **Числа:** —
- **Механіка:** Дає другу зброю — шотган (3 кулі, шкода x0.4, швидкість x3.5, магазин 8); перемикання Q/E (WeaponsManager).
- **Примітки:** Бот не перемикає зброю — картка марна для бота, доки бот не навчиться Q/E.

### Split
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `damageMult`=0.75, `reloadTimeAddSec`=-0.5, `ammoAdd`=3
- **Механіка:** +3 набої, -0.5 с перезарядки, шкода x0.75.

### Twin Scythe
- **Рідкість:** Common · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, aura, defensive
- **Числа:** `orbitRadius`=2, `scytheDamage`=95
- **Механіка:** Навколо гравця обертається коса (радіус орбіти 2): 95 шкоди x2^(n-1), знищує кулі. Макс. 4 коси.
- **Примітки:** Знищення куль — сильний захист навіть на дистанції. Не з'являється, якщо встановлено RSClasses.

## KeysCards (Keys) — 70 карток

Мод Keys-KeysCards. Картки в коді (`CustomCard.BuildCard`, 70 шт.); числа — з `SetupCard` (декомпіляція ilspycmd), умовні бонуси — з `*Mono`-класів.

### Alchemist
- **Рідкість:** Hidden · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated, potion
- **Числа:** `blockCooldownMult`=0.8, `healthMult`=1.2
- **Механіка:** Кулдаун зіль 3 с замість 6, тривалість 5 с; HP x1.2, кулдаун блоку x0.8. Потребує 2 різних зілля.

### All or Nothing
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** long-range, risky
- **Числа:** `healthMult`=0.75, `reloadTimeMult`=0.6667, `blockCooldownAddSec`=0.5, `damageMult`=1.55
- **Механіка:** x1.55 шкода (текст +65%), перезарядка x0.67, HP x0.75 (текст -30%), +0.5 с кулдауну блоку.

### Angelic
- **Рідкість:** Common · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility
- **Числа:** `extraJumps`=1, `moveSpeedMult`=1.1, `damageMult`=1.1, `bulletGravityMult`=0.65
- **Механіка:** +1 стрибок, рух x1.1, x1.1 шкода, гравітація кулі x0.65.

### Apollos Quiver
- **Рідкість:** Treasure · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, treasure
- **Числа:** `reloadTimeMult`=0.7143, `attackCooldownMult`=0.7692, `ammoAdd`=3, `afterReload_attackSpeedBonusPct`=50
- **Механіка:** Перезарядка x0.71, постріл x0.77, +3 набої; +50% швидкості атаки 0.5 с після перезарядки.
- **Примітки:** Скарб.

### Bewitching
- **Рідкість:** Uncommon · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, automatic, dot
- **Числа:** `auraRadius`=6, `autoCooldownSec`=5, `hexStacks`=12
- **Механіка:** Кожні 5 с накладає 12 Hex на ворогів у радіусі ~6.

### Binding Chains
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** low
- **Теги:** close-range, aura
- **Числа:** `moveSpeedMult`=1.15, `chainRadius`=10, `breakDistance`=8, `stunSec`=1.4
- **Механіка:** Ланцюги чіпляються до ворогів у радіусі ~10 у прямій видимості; якщо тримаються 1.5 с — оглушення+тиша 1.4 с; рвуться далі 8. Рух x1.15.
- **Примітки:** Радіус ~10 — менше за дистанцію бота 13-16; треба триматися ближче.

### Bottomless Pockets
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range
- **Числа:** `moveSpeedMult`=0.9, `damageMult`=1.4, `ammoAdd`=6
- **Механіка:** +6 набоїв, x1.4 шкода, рух x0.9.

### Boxing Glove
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `bulletSpeedMult`=0.8, `damageMult`=1.25, `knockbackMult`=1.5
- **Механіка:** Відкидка x1.5, x1.25 шкода, швидкість кулі x0.8.
- **Примітки:** Відкидка може виштовхнути суперника за межу арени.

### Burning Binds
- **Рідкість:** Hidden · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, gated
- **Числа:** —
- **Механіка:** Binding Chains тримає вдвічі коротше, але завдає 20 + 30% макс. HP. Потребує Infernal + Binding Chains.

### Cerulean Potion
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, on-block, potion
- **Числа:** `onBlock_healthMult`=1.55, `onBlock_damageMult`=1.55, `buffDurationSec`=3, `potionCooldownSec`=6
- **Механіка:** Блок (кд 6 с): +55% HP і +55% шкоди на 3 с.

### Citrine Potion
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, on-block, potion
- **Числа:** `onBlock_attackSpeedBonusPct`=75, `onBlock_reloadSpeedBonusPct`=100, `buffDurationSec`=3, `potionCooldownSec`=6
- **Механіка:** Блок (кд 6 с): +75% швидкості атаки, +100% швидкості перезарядки на 3 с.

### Cloak and Dagger
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, crit
- **Числа:** `damageMult`=1.25, `moveSpeedMult`=1.15, `critChanceAddPct`=25
- **Механіка:** +25% шансу криту (крит = x2 шкоди), x1.25 шкода, рух x1.15.
- **Примітки:** Клас Critical.

### Cola Potion
- **Рідкість:** Uncommon · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility, on-block, potion
- **Числа:** `buffDurationSec`=3, `potionCooldownSec`=6
- **Механіка:** Блок (кд 6 с): кулі підпалюють, більша мобільність на 3 с.

### Compass
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** low
- **Теги:** defensive
- **Числа:** `healthMult`=1.2
- **Механіка:** HP x1.2; разом із Treasure Map дає випадковий Скарб.
- **Примітки:** '+10% Attack speed' не працює (як у Treasure Map). Ігнорується: statModifiers.attackSpeedMultiplier (не копіюється ApplyCardStats — ефект не працює).

### Coral Potion
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, on-block, potion
- **Числа:** `onBlock_bulletSpeedMult`=1.8, `onBlock_bouncesAdd`=4, `buffDurationSec`=3, `potionCooldownSec`=6
- **Механіка:** Блок (кд 6 с): +80% швидкості й гравітації кулі, +4 відскоки на 3 с.
- **Примітки:** Змінює балістику під час бою — прицілу бота треба читати поточні стати.

### Crimson Potion
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, on-block, potion
- **Числа:** `onBlock_healPctPerSec`=6, `buffDurationSec`=3, `potionCooldownSec`=6
- **Механіка:** Блок (кулдаун 6 с): знімає DoT і лікує 6%/с 3 с.
- **Примітки:** Зілля діють на блок. 2+ різних зілля відкривають Alchemist.

### Cursed Plunder
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** `moveSpeedMult`=0.9, `healthMult`=0.85, `damageMult`=0.85
- **Механіка:** Одразу дає випадковий Скарб; x0.85 шкода, x0.85 HP, рух x0.9.
- **Примітки:** Загалом вигідно (скарби сильні).

### Dancing Bullet
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, bounce
- **Числа:** `bulletGravityMult`=1.3, `bulletSpeedMult`=1.3, `bouncesAdd`=1, `blockCooldownAddSec`=0.25
- **Механіка:** +1 відскок, швидкість кулі x1.3, гравітація кулі x1.3, +0.25 с кулдауну блоку.

### Dead Mans Boots
- **Рідкість:** Treasure · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, treasure, automatic
- **Числа:** `damageMult`=1.5, `moveSpeedMult`=1.2, `autoCooldownSec`=4
- **Механіка:** Кожні 4 с наступний постріл автоматично заряджається (шкода x1.5 за кожну копію); x1.5 шкода, рух x1.2.
- **Примітки:** Скарб.

### Defensive Dice
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, crit
- **Числа:** `healthMult`=1.4, `critChanceAddPct`=25
- **Механіка:** +25% криту, HP x1.4; -0.25 с кулдауну блоку за кожні 25% криту.

### Demeters Bounty
- **Рідкість:** Treasure · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, treasure, on-block
- **Числа:** `perBlockStack_lifeStealAdd`=0.06, `perBlockStack_attackSpeedBonusPct`=5, `perBlockStack_damageBonusPct`=4
- **Механіка:** Кожен блок дає стак: +6% вампіризму, +5% швидкості атаки, +4% шкоди.
- **Примітки:** Скарб. Добре для бота, що часто блокує.

### Draculas Cloak
- **Рідкість:** Treasure · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, treasure, lifesteal
- **Числа:** `damageMult`=1.25, `healthMult`=1.25, `lifeStealAdd`=0.75
- **Механіка:** +0.75 вампіризм, x1.25 шкода, HP x1.25; регенерація залежно від вампіризму.
- **Примітки:** Скарб.

### Druids Blessing
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, automatic
- **Числа:** `healthMult`=1.35, `autoHealPctMaxHp`=6, `autoCooldownSec`=5
- **Механіка:** HP x1.35; кожні 5 с лікує 6% макс. HP.

### Ecliptic Library
- **Рідкість:** Rare · **Стиль:** close-range · **closeRange:** true · **Ризик для бота:** none
- **Теги:** close-range, aura, dot
- **Числа:** `auraRadius`=7
- **Механіка:** Три томи обертаються навколо; гравці ближче ~7 отримують Hex (1% макс. HP за стак за 6 с).

### Expedition
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** —
- **Механіка:** Скарби з невеликим шансом з'являються у виборі карток.
- **Примітки:** Без статів.

### Familiars Friend
- **Рідкість:** Treasure · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, treasure, bounce, dot
- **Числа:** `bouncesAdd`=3, `attackCooldownMult`=0.833, `bulletGravityMult`=1.2, `spreadAdd`=0.05
- **Механіка:** +3 відскоки, постріл x0.83, гравітація кулі x1.2; кулі дають DoT лише гравцям.
- **Примітки:** Скарб.

### Faster Hardware
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, gated, automatic
- **Числа:** `automaticCooldownMult`=0.714
- **Механіка:** Кулдауни Automatic-карток x0.714. Потрібна будь-яка Automatic-картка.

### Feline Frenzy
- **Рідкість:** Hidden · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated, crit
- **Числа:** —
- **Механіка:** Якщо ворога немає в радіусі 12: +25% криту, крит +50% шкоди. За кодом відкривається парою Scaredy Cat + Fortress.
- **Примітки:** Текст картки каже 'Scaredy Cat + Cloak and Dagger', але категорію CatT знімають Fortress/Scaredy Cat.

### Ferocity
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, crit, lifesteal
- **Числа:** `healthMult`=1.2, `lifeStealAdd`=0.3, `critChanceAddPct`=25
- **Механіка:** +25% криту, +0.3 вампіризм, HP x1.2.

### Fortress
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, lifesteal
- **Числа:** `blockCooldownMult`=0.7, `healthMult`=1.3, `reloadTimeAddSec`=0.25, `lifeStealAdd`=0.2
- **Механіка:** HP x1.3, кулдаун блоку x0.7 (текст -20%), +0.2 вампіризм, +0.25 с перезарядки.

### Giants Gauntlet
- **Рідкість:** Treasure · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, treasure
- **Числа:** `healthMult`=2, `regenPctMaxHpPerSec`=2.5
- **Механіка:** HP x2 (текст +75%), регенерація 2.5% HP/с.
- **Примітки:** Скарб.

### Grovetenders Rest
- **Рідкість:** Hidden · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated
- **Числа:** —
- **Механіка:** Slow Start після активації також дає +50% HP, -10% кулдауну, +1 блок. Потребує Grovetenders Shield + Slow Start.
- **Примітки:** Стати на картці не застосовуються напряму.

### Grovetenders Shield
- **Рідкість:** Treasure · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, treasure
- **Числа:** `extraBlocks`=2, `blockCooldownAddSec`=0.5, `healthMult`=1.5
- **Механіка:** +2 блоки, HP x1.5, +0.5 с кулдауну блоку.
- **Примітки:** Скарб.

### Guardian Angel
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, conditional
- **Числа:** `cond_belowHalfHp_maxHealthMult`=2.0, `cond_healPctPer025s`=3, `buffDurationSec`=6
- **Механіка:** Перший раз за раунд нижче 50% HP: на 6 с макс. HP x2 і лікування 3% кожні 0.25 с.

### Heavensent
- **Рідкість:** Hidden · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated
- **Числа:** —
- **Механіка:** Guardian Angel: ще +40% шкоди і тривалість 12 с. Потребує Angelic + Guardian Angel.

### Hexing Reload
- **Рідкість:** Hidden · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated, dot
- **Числа:** `reloadTimeMult`=0.87
- **Механіка:** Після перезарядки 1 с постріли дають потрійний Hex; перезарядка x0.87. Потребує Mending Reload + Spellbound.

### Infernal
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, dot
- **Числа:** `attackCooldownMult`=0.689, `ammoAdd`=-1, `bulletSpeedMult`=1.5, `dotDurationSec`=2
- **Механіка:** Кулі завдають шкоду протягом 2 с; постріл x0.69, швидкість кулі x1.5, -1 набій.

### Iron Carapace
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated
- **Числа:** `healthMult`=1.75, `extraBlocks`=1
- **Механіка:** HP x1.75, +1 блок. Потребує Metamorphosis (забирає її).
- **Примітки:** '-30% Block cooldown' не працює: код ставить block.cooldown, який ApplyCardStats не копіює. Ігнорується: block.cooldown (не копіюється ApplyCardStats — ефект не працює).

### Iron Heart
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated, lifesteal
- **Числа:** `healthMult`=1.5, `moveSpeedMult`=1.2, `lifeStealAdd`=0.4, `regenPerSec`=15
- **Механіка:** HP x1.5, рух x1.2, +15 регенерації, +0.4 вампіризм. Потребує Metamorphosis (забирає її).

### Iron Pincers
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated
- **Числа:** `damageMult`=1.6, `attackCooldownMult`=0.7692, `bulletSpeedMult`=1.3, `ammoAdd`=4
- **Механіка:** x1.6 шкода, постріл x0.77, швидкість кулі x1.3, +4 набої. Потребує Metamorphosis (забирає її).
- **Примітки:** Одна з найсильніших атакувальних карток.

### Magic Trick
- **Рідкість:** Common · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, multi-projectile
- **Числа:** `projectilesAdd`=1, `spreadAdd`=0.05, `ammoAdd`=3, `reloadTimeAddSec`=0.25, `damageMult`=0.7
- **Механіка:** +1 куля (розкид 0.05), +3 набої, шкода x0.7, +0.25 с перезарядки.

### Mending Reload
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive
- **Числа:** `healthMult`=1.2, `reloadTimeMult`=0.85, `healOnReloadFlat`=12, `healOnReloadPctMaxHp`=1.5
- **Механіка:** HP x1.2, перезарядка x0.85; кожна перезарядка лікує 12 + 1.5% макс. HP.

### Merlins Staff
- **Рідкість:** Treasure · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, treasure, multi-projectile
- **Числа:** `knockbackMult`=1.4, `spreadAdd`=0.15, `ammoAdd`=9, `burstsAdd`=3, `bulletGravityMult`=0
- **Механіка:** Черга з 3 куль, +9 набоїв, гравітація кулі 0, відкидка x1.4, розкид 0.15.
- **Примітки:** Скарб. Без гравітації — простий приціл.

### Metamorphosis
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility
- **Числа:** `healthMult`=1.06, `damageMult`=1.06, `regenPerSec`=1.2
- **Механіка:** x1.06 шкода, x1.06 HP, +1.2 регенерації (текст: +0.6). Відкриває Iron-картки.

### Mirror
- **Рідкість:** Uncommon · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** low
- **Теги:** defensive
- **Числа:** `blockCooldownMult`=0.5, `healthMult`=0.7
- **Механіка:** Кулдаун блоку x0.5, HP x0.7.

### Moonwalker
- **Рідкість:** Uncommon · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** mobility, long-range
- **Числа:** `moveSpeedMult`=0.8, `jumpMult`=1.2, `playerGravityMult`=0.162, `damageMult`=1.35, `bulletGravityMult`=0.162, `bulletSpeedMult`=0.8
- **Механіка:** Гравітація гравця і кулі x0.162; x1.35 шкода; рух x0.8, стрибок x1.2, швидкість кулі x0.8.
- **Примітки:** Низька гравітація ламає навігацію/стрибки бота (A* розрахований на звичайну гравітацію).

### New Sneakers
- **Рідкість:** Common · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility
- **Числа:** `moveSpeedMult`=1.15, `jumpMult`=1.1, `attackCooldownMult`=0.7407
- **Механіка:** Постріл x0.74, рух x1.15, стрибок x1.1.

### Nimble
- **Рідкість:** Common · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility, utility
- **Числа:** `moveSpeedMult`=1.1, `reloadTimeMult`=0.8696, `attackCooldownMult`=0.8333, `bulletSpeedMult`=1.25
- **Механіка:** Рух x1.1, перезарядка x0.87, постріл x0.83, швидкість кулі x1.25.
- **Примітки:** Чисті плюси.

### Nimble+
- **Рідкість:** Common · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility, gated, automatic
- **Числа:** `moveSpeedMult`=1.15, `reloadTimeMult`=0.8333, `attackCooldownMult`=0.8333, `automaticCooldownMult`=0.869
- **Механіка:** Рух x1.15, перезарядка x0.83, постріл x0.83; кулдауни Automatic x0.87. Потрібна Automatic-картка.

### Nuclear Reactor
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility, gated, automatic
- **Числа:** `damageMult`=0.85, `healthMult`=0.85, `automaticCooldownMult`=0.606
- **Механіка:** Кулдауни Automatic x0.606; x0.85 шкода, x0.85 HP. Потрібна Automatic-картка.

### Overcrit
- **Рідкість:** Hidden · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated, crit
- **Числа:** `critChanceAddPct`=25
- **Механіка:** +25% криту; макс. шанс криту 200% (понад 100% — ще x1.5). Відкривається при криті >= 100%.

### Overdrive
- **Рідкість:** Uncommon · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** utility, gated, automatic
- **Числа:** `attackCooldownMult`=0.7143, `automaticCooldownMult`=0.833
- **Механіка:** Постріл x0.71; кулдауни Automatic x0.83. Потрібна Automatic-картка.
- **Примітки:** '-15% Block cooldown' не працює (block.cooldown не копіюється). Ігнорується: block.cooldown (не копіюється ApplyCardStats — ефект не працює).

### Pickpockets Dagger
- **Рідкість:** Treasure · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, treasure, crit
- **Числа:** `attackCooldownMult`=0.6666, `moveSpeedMult`=1.1, `critChanceAddPct`=50
- **Механіка:** +50% криту, крит +25% шкоди, постріл x0.67, рух x1.1.
- **Примітки:** Скарб.

### Pipsqueak
- **Рідкість:** Rare · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** mobility
- **Числа:** `moveSpeedMult`=1.18, `jumpMult`=1.22, `playerGravityMult`=1.13, `healthMult`=0.7, `playerSizeMult`=0.75, `bulletSimSpeedMult`=1.2, `damageMult`=0.85, `attackCooldownMult`=0.75
- **Механіка:** Розмір x0.75, рух x1.18, стрибок x1.22, гравітація x1.13, HP x0.7, шкода x0.85, постріл x0.75, симуляція кулі x1.2.
- **Примітки:** Взаємовиключна з Raid Boss. Менша ціль.

### Precision
- **Рідкість:** Common · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, crit
- **Числа:** `attackCooldownMult`=1.15, `critChanceAddPct`=50
- **Механіка:** +50% шансу криту (x2 шкоди), постріл x1.15.

### Raid Boss
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** defensive, close-range
- **Числа:** `moveSpeedMult`=0.6, `playerGravityMult`=0.65, `healthMult`=3.2, `playerSizeMult`=1.35, `bulletSimSpeedMult`=0.6, `damageMult`=1.3, `attackCooldownMult`=1.25
- **Механіка:** HP x3.2, розмір x1.35, рух x0.6, гравітація гравця x0.65, x1.3 шкода, постріл x1.25, симуляція кулі x0.6.
- **Примітки:** Взаємовиключна з Pipsqueak. Великий і повільний — легка ціль, але дуже живучий.

### Saw Shot
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, automatic
- **Числа:** `autoCooldownSec`=8
- **Механіка:** Кожні 8 с автоматично летить пилка до найближчого ворога (37.5 од/с, 18 + 4*damage шкоди, розмір 2.2, живе 4 с).
- **Примітки:** Автоматична — нічого не вимагає від бота. Дуже добра для бота.

### Scaredy Cat
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, conditional
- **Числа:** `healthMult`=0.75, `triggerRadius`=12, `cond_noEnemyNear_damageMult`=1.5, `cond_noEnemyNear_attackCooldownMult`=0.667, `cond_enemyNear_moveSpeedMult`=1.75
- **Механіка:** HP x0.75. Якщо ворога НЕМАЄ в радіусі 12: x1.5 шкода, постріл x0.67. Якщо є: рух x1.75, стрибок x1.25.
- **Примітки:** Ідеально для бота, що тримає дистанцію >12.

### Sentry
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** high
- **Теги:** long-range, needs-input, conditional
- **Числа:** `blockCooldownMult`=0.75
- **Механіка:** Блок присівши (вниз) перемикає Sentry Mode: шкода x0.35, постріл x0.18 (у ~5.5 раза частіше), розкид, гравітація x0.2, рух x0.2, стрибок x0.4. Кулдаун блоку x0.75.
- **Примітки:** Бот не присідає під час блоку, тож отримує лише -25% кулдауну. Якщо випадково увімкне — стоятиме майже нерухомо.

### Slow Start
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, automatic, conditional
- **Числа:** `cond_afterSec`=12, `cond_damageMult`=1.5, `cond_attackSpeedBonusPct`=30, `cond_moveSpeedBonusPct`=20
- **Механіка:** Через 12 с після старту раунду: x1.5 шкода, +30% швидкості атаки, +20% руху до кінця раунду.

### Snipers Nest
- **Рідкість:** Rare · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** low
- **Теги:** long-range
- **Числа:** `reloadTimeMult`=1.4, `damageMult`=1.35, `bulletSpeedMult`=2.5, `bulletGravityMult`=0.4, `setsMaxAmmoTo`=1, `damageAddPerLostAmmo`=0.14
- **Механіка:** Магазин стає 1 набій; +0.14 шкоди за кожен втрачений набій; x1.35 шкода, x2.5 швидкість кулі, гравітація x0.4, перезарядка x1.4.
- **Примітки:** Добре для точного бота; погано з картками на набої/шотган.

### Spellbound
- **Рідкість:** Uncommon · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, on-hit, dot
- **Числа:** `hexStacksPerHit`=3
- **Механіка:** Кулі накладають 3 Hex (кожен — 1% макс. HP за 6 с).
- **Примітки:** Добре проти танків.

### The Eggs Blessing
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** none
- **Теги:** defensive, gated
- **Числа:** `healthMult`=1.1, `damageMult`=1.1
- **Механіка:** x1.1 шкода, x1.1 HP. Видається з The Golden Egg.

### The Golden Egg
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** —
- **Механіка:** Через 3 раунди «вилуплюється»: дає набір випадкових карток/скарбів + The Eggs Blessing.
- **Примітки:** Нічого не дає одразу.

### Thorn Shot
- **Рідкість:** Hidden · **Стиль:** long-range · **closeRange:** false · **Ризик для бота:** none
- **Теги:** long-range, gated, automatic
- **Числа:** —
- **Механіка:** Saw Shot: кулдаун 12 с, але пилка розміром 3 і 22 + 6*damage шкоди. Потребує Druids Blessing + Saw Shot.

### Treasure Map
- **Рідкість:** Common · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** low
- **Теги:** defensive
- **Числа:** `healthMult`=1.1
- **Механіка:** HP x1.1; разом із Compass дає випадковий Скарб.
- **Примітки:** '+20% Attack speed' на картці не працює: statModifiers.attackSpeedMultiplier не копіюється ApplyCardStats. Ігнорується: statModifiers.attackSpeedMultiplier (не копіюється ApplyCardStats — ефект не працює).

### Treasure Trove
- **Рідкість:** Rare · **Стиль:** utility · **closeRange:** false · **Ризик для бота:** low
- **Теги:** utility
- **Числа:** —
- **Механіка:** Наступний вибір карток складається тільки зі Скарбів (Treasure).
- **Примітки:** Без статів зараз, але скарби дуже сильні.

### Vampiric Block
- **Рідкість:** Rare · **Стиль:** defensive · **closeRange:** false · **Ризик для бота:** high
- **Теги:** defensive, self-damage
- **Числа:** `blockCooldownMult`=0.25, `selfDamagePctMaxHpPerExtraBlock`=8
- **Механіка:** Кулдаун блоку x0.25; після 3 блоків поспіль кожен наступний блок забирає 8% макс. HP x (стаки-3), скидається за 3 с без блоку.
- **Примітки:** Бот, що часто блокує, вбиватиме себе. Брати тільки якщо бот обмежує частоту блоку.

### Warpath
- **Рідкість:** Uncommon · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** none
- **Теги:** mobility, long-range
- **Числа:** `moveSpeedMult`=1.1, `damagePerBonusMoveSpeed`=1.3
- **Механіка:** Рух x1.1; шкода x(1 + 1.3*(рух-1)) за бонус швидкості руху.
- **Примітки:** Синергія з картками на швидкість руху.

### Zephyrs Blessing
- **Рідкість:** Treasure · **Стиль:** mobility · **closeRange:** false · **Ризик для бота:** medium
- **Теги:** mobility, treasure, conditional
- **Числа:** `playerGravityMult`=0.7, `extraJumps`=2, `moveSpeedMult`=1.2, `cond_inAir_damageBonusPct`=40, `cond_inAir_attackSpeedBonusPct`=40
- **Механіка:** +2 стрибки, гравітація гравця x0.7, рух x1.2; у повітрі +40% шкоди і швидкості атаки.
- **Примітки:** Скарб. Текст '+20% Damage' у коді не встановлено. Низька гравітація заважає навігації бота.

## Не в пулі (не рахуються)

- **Remnant** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Блок ставить статичний залишок, що вибухає поруч із ворогом.
- **Ensnare** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Кулі заплутують ціль поблизу.
- **DivineCountdown** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Тестова версія Abyssal Countdown (desc 'ehhhh').
- **BlackHole** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Блок створює чорну діру, що притягує.
- **Frenzy** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +400% ATKSPD, +6 набоїв, -60% шкоди.
- **Overcharge** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +100% швидкості кулі, x3.5 віддача.
- **MultiShot** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +6 куль, +9 набоїв, -70% шкоди.
- **Strength** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +70% шкоди.
- **More ammo** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +6 набоїв.
- **Bash_DMG** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Кулі оглушують.
- **Ammo reg** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. +0.5 регенерації набоїв.
- **0. PlainCard** (Vanilla) — prefab у resources.assets, відсутній у CardChoice.cards. Порожній шаблон.
- **Repentence** (RSCards (RS_Mind)) — першоквітнева/прихована. Першоквітнева версія Repentance (реєструється лише в тиждень 1 квітня); додає Repen10ce.
- **Nothing Field** (RSCards (RS_Mind)) — першоквітнева/прихована. Першоквітнева картка: поле без шкоди.
- **Repen10ce** (RSCards (RS_Mind)) — першоквітнева/прихована. Прихована першоквітнева: x10 шкоди, HP x0.02.
