# -*- coding: utf-8 -*-
import json
import codecs
import sys
sys.stdout = codecs.getwriter('utf-8')(sys.stdout)

with open('Assets/StreamingAssets/Tables/tribe_config.json', 'r') as f:
    tribe_config = json.load(f)

tribes_by_type = {}
for tribe in tribe_config['tribes']:
    tribes_by_type[tribe['tribeType']] = tribe

# Player growth model
initial_cats = {0: 3, 1: 5, 2: 2, 3: 4, 4: 4, 5: 3}
AVG_CAT_MULT = 0.25

def calc_stats(tt, buffs=None, cat_mult=AVG_CAT_MULT, is_cat=False):
    t = tribes_by_type[tt]
    s = t['catBaseStats'] if is_cat else t['leaderBaseStats']
    atk, df, hp, spd = float(s['attack']), float(s['defense']), float(s['hp']), float(s['speed'])
    if is_cat:
        atk *= cat_mult; df *= cat_mult; hp *= cat_mult; spd *= cat_mult
    if buffs:
        atk *= (1 + buffs.get('atk_pct', 0))
        df *= (1 + buffs.get('def_pct', 0))
        hp *= (1 + buffs.get('hp_pct', 0))
        spd *= (1 + buffs.get('spd_pct', 0))
        atk += buffs.get('atk_flat', 0)
        df += buffs.get('def_flat', 0)
        hp += buffs.get('hp_flat', 0)
    return (max(1, int(atk)), max(0, int(df)), max(1, int(hp)))

def build_player_units(tribes_owned, leader_buffs, cat_counts, avg_quality):
    units = []
    for tt in tribes_owned:
        atk, df, hp = calc_stats(tt, leader_buffs.get(tt, {}))
        units.append({'atk': atk, 'def': df, 'hp': hp})
        n = cat_counts.get(tt, 0)
        for _ in range(n):
            cat_atk, cat_def, cat_hp = calc_stats(tt, None, avg_quality, is_cat=True)
            units.append({'atk': cat_atk, 'def': cat_def, 'hp': cat_hp})
    return units

import random

rounds_def = [
    {"bn": 1, "recruit": False, "ritual": False, "shop": False, "newTribe": False, "reward": 100},
    {"bn": 2, "recruit": True,  "ritual": False, "shop": True,  "newTribe": False, "reward": 120},
    {"bn": 3, "recruit": True,  "ritual": True,  "shop": False, "newTribe": False, "reward": 140},
    {"bn": 4, "recruit": True,  "ritual": False, "shop": True,  "newTribe": True,  "reward": 160},
    {"bn": 5, "recruit": True,  "ritual": True,  "shop": False, "newTribe": False, "reward": 180},
    {"bn": 6, "recruit": True,  "ritual": False, "shop": True,  "newTribe": False, "reward": 200},
    {"bn": 7, "recruit": True,  "ritual": True,  "shop": False, "newTribe": False, "reward": 220},
    {"bn": 8, "recruit": True,  "ritual": False, "shop": True,  "newTribe": True,  "reward": 240},
    {"bn": 9, "recruit": True,  "ritual": True,  "shop": False, "newTribe": False, "reward": 260},
    {"bn": 10,"recruit": True,  "ritual": False, "shop": True,  "newTribe": False, "reward": 280},
]

# Simulate player growth
random.seed(42)
tribes_owned = [1, 5]
leader_buffs = {1: {}, 5: {}}
cat_counts = {1: 5, 5: 3}
cat_food = 0
avg_quality = 0.15

print(u"=" * 90)
print(u"Player growth + manual enemy design")
print(u"=" * 90)

# Design enemies manually
# Leaders: ATK ~85-130, DEF ~70-100, HP ~850-1100
# Cats (white 15%): ATK ~9-14, DEF ~7-11, HP ~77-113
# Cats (blue 25%): ATK ~15-23, DEF ~12-18, HP ~128-190
# Cats (purple 35%): ATK ~21-32, DEF ~17-25, HP ~179-266
#
# Requirements: cat can deal damage (10-30), enemy can kill cat, both sides meaningful
# enemy DEF must be LOW (3-8) so cats do atk-DEF damage
# enemy ATK must be HIGH enough to threaten leaders (above leader DEF)
# enemy HP high enough that leaders need 3-5 hits, cats need 5-15 hits

# Enemy count and stats per round
enemy_defs = [
    {"count": 3,  "atk": 85,  "def": 3,  "hp": 400,  "spd": 2.0},   # 1: cat hits ~8, leader ~105
    {"count": 5,  "atk": 90,  "def": 4,  "hp": 450,  "spd": 2.05},  # 2
    {"count": 7,  "atk": 95,  "def": 5,  "hp": 500,  "spd": 2.1},   # 3
    {"count": 8,  "atk": 100, "def": 5,  "hp": 550,  "spd": 2.15},  # 4
    {"count": 10, "atk": 105, "def": 6,  "hp": 600,  "spd": 2.2},   # 5
    {"count": 12, "atk": 110, "def": 6,  "hp": 650,  "spd": 2.25},  # 6
    {"count": 14, "atk": 115, "def": 7,  "hp": 700,  "spd": 2.3},   # 7
    {"count": 15, "atk": 120, "def": 7,  "hp": 750,  "spd": 2.35},  # 8
    {"count": 17, "atk": 125, "def": 8,  "hp": 800,  "spd": 2.4},   # 9
    {"count": 18, "atk": 130, "def": 8,  "hp": 850,  "spd": 2.45},  # 10
]

# enemy_ids just for type variety (doesn't affect stats)
enemy_id_sets = [
    [1,1,1],
    [1,1,1,1,1],
    [1,1,1,2,2,2,2],
    [1,1,1,2,2,2,2,2],
    [1,1,1,2,2,2,2,2,3,3],
    [1,1,1,2,2,2,2,2,3,3,3,3],
    [1,1,1,2,2,2,2,2,2,3,3,3,3,3],
    [1,1,1,2,2,2,2,2,2,3,3,3,3,3,3],
    [1,1,1,1,2,2,2,2,2,2,3,3,3,3,3,3,3],
    [1,1,1,1,2,2,2,2,2,2,3,3,3,3,3,3,3,3],
]

results = []

for i, lv in enumerate(rounds_def):
    bn = lv['bn']
    units = build_player_units(tribes_owned, leader_buffs, cat_counts, avg_quality)
    p_hp = sum(u['hp'] for u in units)
    n_leaders = len(tribes_owned)
    n_cats = sum(cat_counts.get(t, 0) for t in tribes_owned)

    ed = enemy_defs[i]
    e_count = ed['count']
    e_atk = ed['atk']
    e_def = ed['def']
    e_hp = ed['hp']
    e_spd = ed['spd']

    # Calculate per-hit damage
    leaders = [u for u in units if u['atk'] > 50]
    cats = [u for u in units if u['atk'] <= 50]

    leader_dmg = max(1, leaders[0]['atk'] - e_def) if leaders else 0
    enemy_dmg_to_leader = max(1, e_atk - leaders[0]['def']) if leaders else 0
    cat_dmg = max(1, cats[0]['atk'] - e_def) if cats else 0
    enemy_dmg_to_cat = max(1, e_atk - cats[0]['def']) if cats else 0

    # Estimate time to kill
    hits_to_kill_enemy = float(e_hp) / max(1, leader_dmg)
    hits_to_kill_cat = float(cats[0]['hp']) / max(1, enemy_dmg_to_cat) if cats else 999
    hits_to_kill_leader = float(leaders[0]['hp']) / max(1, enemy_dmg_to_leader) if leaders else 999

    print(u"Bat {:>2}: {:>2} enm eATK={:>3} eDEF={:>2} eHP={:>3} | "
          u"{}L {}C HP={:.0f} | "
          u"leader={}dmg/enm={}dmg({:.0f}hit) cat={}dmg/enm={}dmg({:.0f}hit)".format(
        bn, e_count, e_atk, e_def, e_hp,
        n_leaders, n_cats, p_hp,
        leader_dmg, enemy_dmg_to_leader, hits_to_kill_leader,
        cat_dmg, enemy_dmg_to_cat, hits_to_kill_cat))

    results.append({
        "battleNumber": bn,
        "enemyUnitIds": enemy_id_sets[i],
        "catFoodReward": lv['reward'],
        "hasRecruitment": lv['recruit'],
        "hasRitual": lv['ritual'],
        "hasShop": lv['shop'],
        "hasNewTribeEvent": lv['newTribe'],
        "enemyStats": {
            "attack": e_atk,
            "defense": e_def,
            "hp": e_hp,
            "moveSpeed": e_spd,
            "attackRange": 1.0
        }
    })

    # Apply growth
    random.seed(bn * 77)
    cat_food += lv['reward']

    if lv['recruit']:
        target = tribes_owned[bn % len(tribes_owned)]
        if target not in leader_buffs:
            leader_buffs[target] = {}
        b = leader_buffs[target]
        chosen = random.choice(['atk_pct', 'def_pct', 'hp_pct', 'spd_pct'])
        b[chosen] = b.get(chosen, 0) + 0.20
        add_target = random.choice(tribes_owned)
        add_n = tribes_by_type[add_target]['initialCatCount']
        cat_counts[add_target] = cat_counts.get(add_target, 0) + add_n
        avg_quality = min(0.35, avg_quality + 0.015)

    if lv['ritual']:
        if cat_food >= 500:
            target = random.choice(tribes_owned)
            n_rit = random.randint(2, 5)
            cat_counts[target] = cat_counts.get(target, 0) + n_rit
            cat_food -= 500; cat_food += 550
            avg_quality = min(0.38, avg_quality + 0.02)
        elif cat_food >= 100:
            target = random.choice(tribes_owned)
            n_rit = random.randint(1, 3)
            cat_counts[target] = cat_counts.get(target, 0) + n_rit
            cat_food -= 100; cat_food += 200
            avg_quality = min(0.32, avg_quality + 0.01)
        else:
            target = random.choice(tribes_owned)
            n_rit = random.randint(1, 3)
            cat_counts[target] = cat_counts.get(target, 0) + n_rit
            cat_food += 100

    if lv['shop']:
        n_buy = random.randint(1, 2)
        for _ in range(n_buy):
            if cat_food >= 120:
                target = random.choice(tribes_owned)
                cat_counts[target] = cat_counts.get(target, 0) + 1
                cat_food -= 120
                avg_quality = min(0.35, avg_quality + 0.005)

    if lv['newTribe'] and len(tribes_owned) < 6:
        available = [t for t in range(6) if t not in tribes_owned]
        new_tribe = available[0]
        tribes_owned.append(new_tribe)
        cat_counts[new_tribe] = initial_cats[new_tribe]
        leader_buffs[new_tribe] = {}
    elif lv['newTribe']:
        cat_food += 1000

    cat_food = max(0, cat_food + random.randint(-50, 50))

# Output JSON
output = {
    "popupPriorities": {"newTribeEvent": 30, "recruitment": 20, "ritual": 10, "shop": 0},
    "levels": results
}
print(u"\nJSON:")
print(json.dumps(output, indent=2, ensure_ascii=False))
