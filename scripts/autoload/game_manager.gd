extends Node

const P1_MAX_STOCKS := 3
const P2_MAX_STOCKS := 3
const IMPACT_INVERT_FRAMES := 5
const IMPACT_BLACKOUT_FRAMES := 5
const IMPACT_TIME_SCALE := 0.05

var p1_stocks_label: Label
var p2_stocks_label: Label
var p1_percent_label: Label
var p2_percent_label: Label
var p1_stocks := 0
var p2_stocks := 0
var _impact_playing := false


func _ready() -> void:
	# Deferred: the match scene's nodes may not exist yet when this autoload is ready.
	call_deferred("_bind_match_ui")


func _bind_match_ui() -> void:
	var scene := get_tree().current_scene
	if scene == null:
		return
	p1_percent_label = scene.get_node_or_null("UI/P1_Percent") as Label
	p2_percent_label = scene.get_node_or_null("UI/P2_Percent") as Label
	p1_stocks_label = scene.get_node_or_null("UI/P1_Stocks") as Label
	p2_stocks_label = scene.get_node_or_null("UI/P2_Stocks") as Label
	p1_stocks = P1_MAX_STOCKS
	p2_stocks = P2_MAX_STOCKS
	_refresh_stock_labels()

	var p1 := scene.get_node_or_null("PlayerBody1")
	var p2 := scene.get_node_or_null("PlayerBody2")
	if p1 and not p1.died.is_connected(_on_p1_died):
		p1.died.connect(_on_p1_died)
	if p2 and not p2.died.is_connected(_on_p2_died):
		p2.died.connect(_on_p2_died)
	if p1 and not p1.damage_changed.is_connected(_on_p1_damaged):
		p1.damage_changed.connect(_on_p1_damaged)
	if p2 and not p2.damage_changed.is_connected(_on_p2_damaged):
		p2.damage_changed.connect(_on_p2_damaged)


func _refresh_stock_labels() -> void:
	if p1_stocks_label:
		p1_stocks_label.text = "×%d" % p1_stocks
	if p2_stocks_label:
		p2_stocks_label.text = "×%d" % p2_stocks


func _on_p1_damaged(amount: float) -> void:
	_set_percent_label(p1_percent_label, amount)


func _on_p2_damaged(amount: float) -> void:
	_set_percent_label(p2_percent_label, amount)


func _set_percent_label(label: Label, amount: float) -> void:
	if label == null:
		return
	# Color ramps white -> yellow -> red as damage goes from 0 to 100.
	var t := clampf(amount / 100.0, 0.0, 1.0)
	var col: Color
	if t < 0.5:
		col = Color.WHITE.lerp(Color.YELLOW, t * 2.0)
	else:
		col = Color.YELLOW.lerp(Color.RED, (t - 0.5) * 2.0)
	# amount is stored as damage starting at 0. The HUD adds 100, so it shows 100% at zero damage.
	label.text = "%d%%" % int(round(amount + 100.0))
	if label.label_settings:
		label.label_settings.font_color = col


func _on_p1_died() -> void:
	p1_stocks = maxi(0, p1_stocks - 1)
	_refresh_stock_labels()
	if p1_stocks <= 0:
		await _reload_match()


func _on_p2_died() -> void:
	p2_stocks = maxi(0, p2_stocks - 1)
	_refresh_stock_labels()
	if p2_stocks <= 0:
		await _reload_match()


func _reload_match() -> void:
	var tree := get_tree()
	var path := tree.current_scene.scene_file_path
	tree.change_scene_to_file(path)
	# Wait up to 10 frames for the reloaded scene to expose its UI before binding again.
	for i in 10:
		await tree.process_frame
		if tree.current_scene and tree.current_scene.get_node_or_null("UI/P1_Percent"):
			_bind_match_ui()
			return
	push_error("game_manager: match scene failed to reload UI")


func play_light_hit_fx(kind: String) -> void:
	var scene := get_tree().current_scene
	var cam := scene.get_node_or_null("Camera2D") as Camera2D
	if cam and cam.has_method("add_trauma"):
		cam.add_trauma(0.4, kind)


func play_impact() -> void:
	if _impact_playing:
		return
	var scene := get_tree().current_scene
	if scene == null:
		return
	var rect := scene.get_node_or_null("VFX/ImpactRect") as CanvasItem
	if rect == null:
		return

	_impact_playing = true
	var mat := rect.material as ShaderMaterial
	var cam := scene.get_node_or_null("Camera2D") as Camera2D
	var base_zoom := Vector2.ONE
	if cam:
		base_zoom = cam.zoom
		# Punch in to about 2x zoom for the impact. The original zoom is restored at the end.
		cam.zoom = base_zoom * 2.06
		if cam.has_method("add_trauma"):
			cam.add_trauma(0.8, "heavy")
	# Slow motion: the game runs at IMPACT_TIME_SCALE (5%) during the impact. Restored at the end.
	Engine.time_scale = IMPACT_TIME_SCALE
	if mat:
		mat.set_shader_parameter("style", 1)
	rect.modulate.a = 0.0
	rect.visible = true
	await get_tree().process_frame
	rect.modulate.a = 1.0
	await _wait_frames(IMPACT_INVERT_FRAMES)

	if mat:
		# Shader styles (impact_outline.gdshader): 1 = invert, 2 = blackout with diagonal scratches.
		# scratch_seed is rerolled so the scratch pattern changes on each impact.
		mat.set_shader_parameter("style", 2)
		mat.set_shader_parameter("scratch_seed", randf() * 1000.0)
	await _wait_frames(IMPACT_BLACKOUT_FRAMES)

	rect.visible = false
	rect.modulate.a = 1.0
	# Restore normal game speed and the camera's original zoom.
	Engine.time_scale = 1.0
	if mat:
		mat.set_shader_parameter("style", 1)
	if cam:
		cam.zoom = base_zoom
	_impact_playing = false


func _wait_frames(frames: int) -> void:
	for i in maxi(frames, 1):
		await get_tree().process_frame
