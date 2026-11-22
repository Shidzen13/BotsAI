"""Підсумок прогону ботів з botlog.txt (запуск: python tools/analyze_run.py [botlog] [з_рядка]).

Рахує смерті за причиною, ботом і варіантом A/B (по матчах), частку смертей після застрягання,
STUCK за хвилину і частку path=0 — метрики кроку 1 плану ШІ-чату (2026-11-22).
"""
import collections
import io
import os
import re
import sys

DEFAULT_LOG = os.path.join(os.environ.get("APPDATA", ""), r"r2modmanPlus-local\ROUNDS\profiles\Dev\BepInEx\RoundsBotsRecordings\botlog.txt")
FIELD = re.compile(r"(\w+)=(\S+)")


def to_seconds(stamp):
    h, m, s = (int(x) for x in stamp.split(":"))
    return h * 3600 + m * 60 + s


def main():
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_LOG
    start = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.readlines()[start - 1:]

    deaths, stucks, decisions, zero_paths, stamps = [], 0, 0, 0, []
    last_void = {}  # bot -> (час, поля останнього VOID_FALL)
    bottom_groups = collections.Counter()
    for line in lines:
        stamp = line[:8]
        now = to_seconds(stamp) if re.match(r"\d\d:\d\d:\d\d", stamp) else None
        if now is not None:
            stamps.append(now)
        if " VOID_FALL " in line:
            fields = dict(FIELD.findall(line))
            last_void[fields.get("bot")] = (now, fields)
        if "RESULT died" in line and "cause=" in line:
            death = dict(FIELD.findall(line))
            deaths.append(death)
            if death.get("cause") == "bounds" and death.get("pos", "(0,0)").rstrip(")").split(",")[-1].startswith("-"):
                seen = last_void.get(death.get("bot"))
                if seen is None or now is None or seen[0] is None or not 0 <= now - seen[0] <= 6:
                    bottom_groups["без VOID_FALL"] += 1
                elif seen[1].get("planned") == "Bounce":
                    bottom_groups["план Bounce"] += 1
                elif seen[1].get("blockCD") == "True":
                    bottom_groups["блок на CD"] += 1
                else:
                    bottom_groups["блок готовий"] += 1
        elif " STUCK " in line:
            stucks += 1
        elif " path=" in line:
            decisions += 1
            zero_paths += " path=0 " in line

    minutes = max(1e-9, (stamps[-1] - stamps[0]) / 60) if len(stamps) > 1 else 1e-9
    if minutes < 0:
        minutes += 24 * 60  # прогін через північ
    print(f"рядків: {len(lines)}, тривалість ≈ {minutes:.0f} хв, матчів: {len({d.get('match') for d in deaths})}")
    print(f"смертей: {len(deaths)}; STUCK: {stucks} ({stucks / minutes:.1f}/хв); path=0: {zero_paths}/{decisions}"
          f" ({100 * zero_paths / max(1, decisions):.0f}%)")
    for title, key in (("причиною", "cause"), ("ботом", "bot"), ("варіантом", "variant")):
        counts = collections.Counter(d.get(key, "?") for d in deaths)
        print(f"  за {title}: " + ", ".join(f"{k}={v}" for k, v in counts.most_common()))
    stuck_deaths = sum(d.get("stuck2s") == "True" for d in deaths)
    print(f"  застряг ≤2 с до смерті: {stuck_deaths} ({100 * stuck_deaths / max(1, len(deaths)):.0f}%)")
    lives = sorted(float(d["life"]) for d in deaths if "life" in d)
    if lives:
        print(f"  час життя: медіана {lives[len(lives) // 2]:.1f} с")
    if bottom_groups:
        print("  смерті на дні: " + ", ".join(f"{k}={v}" for k, v in bottom_groups.most_common()))
    bullet_share = sum(d.get("cause") == "player" for d in deaths) / max(1, len(deaths))
    print(f"  поріг плану: від куль {100 * bullet_share:.0f}% (>60%?), застрягли {100 * stuck_deaths / max(1, len(deaths)):.0f}% (<20%?)")


if __name__ == "__main__":
    main()
