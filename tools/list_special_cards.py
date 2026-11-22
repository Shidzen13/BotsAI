"""Список карток, чиї особливі ефекти не покриває базова оцінка (CardScoring.Score) — для ручної оцінки specialValue.

Запуск: python tools/list_special_cards.py > docs/special_cards.txt
"""
import io
import json
import math
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
MAPPED = {
    "damageMult", "attackCooldownMult", "reloadTimeMult", "reloadTimeAddSec", "ammoAdd", "projectilesAdd",
    "healthMult", "moveSpeedMult", "lifeStealAdd", "regenPerSec", "blockCooldownMult", "blockCooldownAddSec",
    "extraBlocks", "bouncesAdd", "playerSizeMult", "extraJumps", "respawnsAdd",
}


def log(value: float) -> float:
    return math.log(max(value, 0.01))


def base_score(card: dict) -> float:
    e = card.get("effects", {})
    rarity = {"common": 0, "uncommon": 1, "rare": 2}.get(str(card.get("rarity", "")).lower(), 0)
    reload_mult = e.get("reloadTimeMult", 1.0) * (1.0 + e.get("reloadTimeAddSec", 0.0) / 2.0)
    block_mult = e.get("blockCooldownMult", 1.0) * (1.0 + e.get("blockCooldownAddSec", 0.0) / 4.0)
    return (rarity * 0.6 + log(e.get("damageMult", 1)) * 3 - log(e.get("attackCooldownMult", 1)) * 2
            - log(reload_mult) * 1.5 + e.get("ammoAdd", 0) * 0.1 + e.get("projectilesAdd", 0) * 0.8
            + log(e.get("healthMult", 1)) * 2.5 + log(e.get("moveSpeedMult", 1)) * 1.5 + e.get("lifeStealAdd", 0) * 2
            + e.get("regenPerSec", 0) * 0.05 + e.get("respawnsAdd", 0) * 3 - log(block_mult) * 1.5
            + e.get("extraBlocks", 0) * 1.2 + e.get("bouncesAdd", 0) * 0.4 - log(e.get("playerSizeMult", 1)) * 2
            + e.get("extraJumps", 0) * 0.8)


def main() -> None:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    cards = json.loads((ROOT / "docs" / "cards.json").read_text(encoding="utf-8"))
    for card in sorted(cards, key=lambda c: c["name"].upper()):
        effects = card.get("effects", {})
        unmapped = {k: v for k, v in effects.items() if k not in MAPPED}
        if not unmapped and not card.get("mechanics"):
            continue
        print(f"{card['name']} [{card.get('mod')}/{card.get('rarity')}] base={base_score(card):.2f} "
              f"special={card.get('specialValue', '-')} risk={card.get('botRisk')}")
        print(f"  unmapped: {json.dumps(unmapped, ensure_ascii=False)}")
        print(f"  mechanics: {card.get('mechanics', '')}")


if __name__ == "__main__":
    main()
