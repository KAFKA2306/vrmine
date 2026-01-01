import json, sys

path = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else None
data = json.load(open(path))
visuals = data.get("visuals", [])
interactors = data.get("interactors", [])
min_foot = float(data.get("minFootprint", 0))
min_height = float(data.get("minHeight", 0))
min_x = max_x = min_y = max_y = min_z = max_z = 0.0
have_bounds = False
visual_ready = 0
for item in visuals:
    if item.get("enabled") and item.get("material"):
        visual_ready += 1
    center = item.get("center")
    size = item.get("size")
    if center is None or size is None:
        continue
    hx = size[0] * 0.5
    hy = size[1] * 0.5
    hz = size[2] * 0.5
    ax = center[0] - hx
    bx = center[0] + hx
    ay = center[1] - hy
    by = center[1] + hy
    az = center[2] - hz
    bz = center[2] + hz
    if not have_bounds:
        min_x = ax
        max_x = bx
        min_y = ay
        max_y = by
        min_z = az
        max_z = bz
        have_bounds = True
    else:
        if ax < min_x:
            min_x = ax
        if bx > max_x:
            max_x = bx
        if ay < min_y:
            min_y = ay
        if by > max_y:
            max_y = by
        if az < min_z:
            min_z = az
        if bz > max_z:
            max_z = bz
footprint = 0.0
height = 0.0
if have_bounds:
    footprint = (max_x - min_x) * (max_z - min_z)
    height = max_y - min_y
size_ready = have_bounds and footprint >= min_foot and height >= min_height
interact_ready = 0
for item in interactors:
    if item.get("enabled") and item.get("active"):
        interact_ready += 1
visual_total = len(visuals)
interact_total = len(interactors)
score_size = 1.0 if size_ready else 0.0
score_visual = visual_ready / visual_total if visual_total > 0 else 0.0
score_interact = interact_ready / interact_total if interact_total > 0 else 0.0
score_total = (score_size + score_visual + score_interact) / 3.0
lines = [
    "Size:" + ("OK" if size_ready else "NG") + "(" + str(round(footprint)) + "/" + str(min_foot) + "," + str(round(height)) + "/" + str(min_height) + ")",
    "Visual:" + str(visual_ready) + "/" + str(visual_total),
    "Interact:" + str(interact_ready) + "/" + str(interact_total),
    "Score:" + str(round(score_total * 100))
]
text = "\n".join(lines)
if out:
    with open(out, "w") as handle:
        handle.write(text)
print(text)
