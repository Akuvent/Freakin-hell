extends Node2D

# Used for any setting missing from defaults.json. Matches the values in the guide.
const FALLBACK_SETTINGS := {
	"glitch_start_seconds": 60,
	"decision_seconds": 100,
	"virus_chance": 0.2,
	"virus_damage_per_second": 2.0,
	"roll_interval_min_seconds": 10,
	"roll_interval_max_seconds": 15,
}

var settings: Dictionary = {}
var platforms: Array = []
var glitch_started := false
var next_roll_time = null
var decided_count := 0
var glitch_tweens: Array = []


# Called when the node enters the scene tree for the first time.
func _ready():
	var game_data: Dictionary = GameManager.game_data
	var section = game_data.get("map_rot")
	if not (section is Dictionary):
		push_error("rot_controller: 'map_rot' section missing from defaults.json, using fallback values")
		section = {}

	for key in FALLBACK_SETTINGS:
		if section.has(key):
			settings[key] = section[key]
		else:
			push_error("rot_controller: '%s' missing from defaults.json, using fallback %s" % [key, FALLBACK_SETTINGS[key]])
			settings[key] = FALLBACK_SETTINGS[key]

	for child in get_children():
		platforms.append(child)
	if platforms.is_empty():
		push_error("rot_controller: no platforms found under RotController")


# The round clock lives in GameManager (GameManager.round_clock). Read it here, don't count it here.
func _process(_delta):
	if not glitch_started and GameManager.round_clock >= settings["glitch_start_seconds"]:
		start_glitch()

# Runs once per round. Does the setup, then hands off to ongoing_glitch().
func start_glitch() -> void:
	if glitch_started:
		return
	glitch_started = true
	ongoing_glitch()

# Keeps every platform flickering until the round resets. Each platform gets its own looping tween.
func ongoing_glitch() -> void:
	for platform in platforms:
		var visual: Polygon2D = platform.get_node("PlatformVisual")
		var tween := create_tween().set_loops()
		tween.tween_property(visual, "modulate", Color(1.0, 0.4, 0.9), 0.15)
		tween.tween_property(visual, "modulate", Color.WHITE, 0.15)
		glitch_tweens.append(tween)

func reset_round():
	glitch_started = false
	next_roll_time = null
	decided_count = 0
	for platform in platforms:
		platform.reset()


func platform_decided(platform, result):
	decided_count = decided_count + 1
