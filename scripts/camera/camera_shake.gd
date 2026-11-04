extends Camera2D

# Trauma model: each hit adds trauma (0 to 1), which decays at 1.2 per second.
# Shake strength is trauma squared times a multiplier (10 by default, 100 for heavy hits),
# so small trauma gives a small shake and large trauma gives a sharp one.

var trauma: float = 0.0
var time: float = 0.0
var strength: float = 0
# Kind of the most recent hit ("light" or "heavy"). Strength uses it until the next add_trauma() call.
var attack_type: String = ""


func add_trauma(amount: float, kind: String) -> void:
	trauma = clampf(trauma + amount, 0.0, 1.0)
	attack_type = kind


func _process(delta: float) -> void:
	if attack_type == "light":
		strength = (trauma * trauma) * 10.0
	elif attack_type == "heavy":
		strength = (trauma * trauma) * 100.0
	else:
		strength = (trauma * trauma) * 10.0
	time += delta
	# Different frequencies on x and y (35 vs 20) make the shake look less like a circle.
	offset.x = sin(time * 35.0) * strength
	offset.y = sin(time * 20.0) * strength
	trauma = maxf(trauma - 1.2 * delta, 0.0)
