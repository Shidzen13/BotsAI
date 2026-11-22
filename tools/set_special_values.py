"""Записує specialValue (бонус до оцінки за особливі ефекти, яких не бачить CardScoring.Score) у docs/cards.json.

Одиниці — ті самі, що й у Score: ~1 = помітно корисна картка. Ризик для бота (botRisk) рахується окремо.
Запуск: python tools/set_special_values.py && python tools/gen_card_knowledge.py
"""
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent

SPECIAL = {
    "ALCHEMIST": 1.2, "ANGELIC": 0.15, "APOLLOS QUIVER": 0.2, "BARRAGE": 0.2, "BEWITCHING": 0.4, "BIG BULLET": 0.5,
    "BINDING CHAINS": 0.6, "BOMBS AWAY": 0.2, "BOUNCE ABSORPTION": 0.3, "BRAWLER": 1.1, "BURNING BINDS": 1.5,
    "BURST": 1.1, "CERULEAN POTION": 0.9, "CHASE": 0.2, "CHILLING PRESENCE": 0.3, "CITRINE POTION": 0.6,
    "CLOAK AND DAGGER": 0.65, "COLA POTION": 0.4, "COLD BULLETS": 1.2, "COMPASS": 0.4, "CORAL POTION": 0.2,
    "CRIMSON POTION": 0.6, "CURSED PLUNDER": 2.0, "DANCING BULLET": 0.1, "DAZZLE": 0.7, "DEAD MANS BOOTS": 0.4,
    "DECAY": 0.5, "DEFENSIVE DICE": 0.75, "DEMETERS BOUNTY": 1.5, "DEMONIC PACT": -6.0, "DRACULAS CLOAK": 0.3,
    "DRILL AMMO": 0.2, "DRUIDS BLESSING": 0.2, "ECLIPTIC LIBRARY": 0.2, "EMP": 0.3, "EMPOWER": 0.6,
    "EXPEDITION": -0.3, "EXPLOSIVE BULLET": 2.8, "FAST FORWARD": 0.6, "FASTBALL": 1.0, "FASTER HARDWARE": 0.0,
    "FELINE FRENZY": 0.7, "FEROCITY": 0.65, "FROST SLAM": 0.4, "GIANTS GAUNTLET": 0.5, "GROVETENDERS REST": 1.1,
    "GROW": 0.9, "GUARDIAN ANGEL": 1.7, "HARMING FIELD": 0.2, "HEALING FIELD": 0.4, "HEAVENSENT": 1.5,
    "HEXING RELOAD": 0.3, "HITSCAN": 2.0, "HOMING": 2.7, "INFERNAL": 0.3, "IRON PINCERS": 0.2, "LIFESTEALER": 0.7,
    "MAGIC TRICK": 0.7, "MAYHEM": -0.4, "MENDING RELOAD": 0.4, "MERLINS STAFF": 1.2, "METAMORPHOSIS": 0.5,
    "NIMBLE": 0.2, "NIMBLE+": 0.1, "NUCLEAR REACTOR": 0.0, "OPEN CHAMBER": -0.1, "OVERCRIT": 0.6, "OVERDRIVE": 0.15,
    "OVERPOWER": 0.3, "PICKPOCKETS DAGGER": 1.45, "PIPSQUEAK": 0.2, "PRECISION": 1.2, "PRISITNE PERSEVERENCE": 0.5,
    "QUICK SHOT": 0.7, "RADAR SHOT": 0.6, "RAID BOSS": -0.4, "REFRESH": 1.0, "REGENERATOR": 0.6, "SAW": 0.5,
    "SAW SHOT": 1.0, "SCAREDY CAT": 1.5, "SCAVENGER": 1.2, "SHIELD CHARGE": 0.2, "SHIELDS UP": 0.3,
    "SHOCKWAVE": 0.4, "SILENCE": 0.7, "SLOW START": 0.9, "SLUG": -0.25, "SNEAKY": 0.2, "SPELLBOUND": 0.3,
    "STATIC FIELD": 0.5, "STEADY SHOT": 0.55, "SUPERNOVA": 0.5, "TACTICAL RELOAD": 0.5, "TARGET BOUNCE": 0.8,
    "TASTE OF BLOOD": 0.3, "THE GOLDEN EGG": 0.8, "THORN SHOT": 0.4, "THRUSTER": 0.4, "TIMED DETONATION": 2.0,
    "TOXIC CLOUD": 2.1, "TREASURE MAP": 0.4, "TRICKSTER": 0.1, "TWIN SCYTHE": 2.3, "WARPATH": 0.5, "WIND UP": 0.55,
    "ZEPHYRS BLESSING": 0.6,
}


def main() -> None:
    path = ROOT / "docs" / "cards.json"
    cards = json.loads(path.read_text(encoding="utf-8"))
    known = {card["name"].upper() for card in cards}
    missing = sorted(set(SPECIAL) - known)
    if missing:
        raise SystemExit(f"unknown cards: {missing}")

    for card in cards:
        value = SPECIAL.get(card["name"].upper())
        if value is None:
            card.pop("specialValue", None)
        else:
            card["specialValue"] = value

    path.write_text(json.dumps(cards, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(len(SPECIAL), "special values set")


if __name__ == "__main__":
    main()
