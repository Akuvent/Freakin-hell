extends CharacterBody2D

#region Movement
const MOVE_SPEED := 150.0
const JUMP_VELOCITY := -350.0
const AIR_CONTROL_SPEED := 560.0 # how fast mid-air velocity moves toward input, per second
const AIR_JUMP_HORIZONTAL_SPEED := 150.0
const FOOTSTEP_INTERVAL := 0.22

@export var jump_charge := 1 # air jumps restored on landing
@export var coyote_time := 0.1
@export var facing_right := true
@export var action_move_left: StringName = &"p1_move_left"
@export var action_move_right: StringName = &"p1_move_right"
@export var action_jump: StringName = &"p1_jump"
@export var action_attack: StringName = &"p1_attack"
@export var action_heavy_attack: StringName = &"p1_heavy_attack"

var jump_charge_left := 0
var coyote_timer := 0.0
var _footstep_timer := 0.0
var spawn_pos: Vector2
#endregion

#region Combat state
enum AttackKind { LIGHT, HEAVY }

const ATTACK_CHARGE_MAX := 5
const HEAVY_ATTACK_CHARGE_COST := 3
const ATTACK_ACTIVE_TIME := 0.1
const MOVE_LOCK_ON_HIT := 0.12
const LIGHT_HITSTOP := 0.1
const LIGHT_HIT_MAX_DAMAGE := 8.0 # hits at or below this are light hits
const HEAVY_IMPACT_MIN_DAMAGE := 9.0 # hits at or above this trigger the impact screen
const HIT_SPARK := preload("res://scenes/vfx/hit_spark.tscn")

var is_winding_up := false # attack startup, before the hitbox is live
var is_attack_active := false # hitbox is live
var _is_hovering_in_air := false # aerial attack: holds position in the air
var _attack_serial := 0 # bumped to cancel attack coroutines that are still waiting
var move_lock_timer := 0.0
var current_hit: HitData
var total_damage := 0.0
var hit_landed := false
var attack_charge := 0
signal died
signal damage_changed
#endregion

#region Node refs
@onready var sprite: AnimatedSprite2D = $CharSprite2D
@onready var attack_area: Area2D = $AttackArea
@onready var attack_collider: CollisionShape2D = $AttackArea/AttackAreaCollider
@onready var attack_sprite: Sprite2D = $AttackArea/AttackAreaSprite
@onready var hurt_box_collision: CollisionShape2D = $HurtBox/HurtBoxCollision
var _collider_offset_x := 0.0 # distance from the body centre to the attack hitbox
var _sprite_offset_x := 0.0 # distance from the body centre to the attack sprite
#endregion

#region Lifecycle
func _ready() -> void:
	spawn_pos = global_position
	_collider_offset_x = absf(attack_collider.position.x)
	_sprite_offset_x = absf(attack_sprite.position.x)
	set_attack_hitbox_active(false)
	_apply_facing()


func _physics_process(delta: float) -> void:
	if move_lock_timer > 0.0:
		move_lock_timer = maxf(0.0, move_lock_timer - delta)

	if _is_hovering_in_air:
		_physics_air_attack_hover()
	elif is_winding_up or is_attack_active:
		_physics_while_attacking(delta)
	elif move_lock_timer > 0.0:
		_physics_move_locked(delta)
	else:
		_physics_free_control(delta)

	move_and_slide()
	_update_footsteps(delta)

	# During wind-up, start_attack() holds the attack animation; don't let locomotion animations override it.
	if not is_winding_up or is_attack_active:
		_update_anims()

	if is_on_floor():
		coyote_timer = coyote_time
		jump_charge_left = jump_charge
#endregion

#region Physics branches
func _physics_air_attack_hover() -> void:
	velocity = Vector2.ZERO


func _physics_while_attacking(delta: float) -> void:
	_apply_gravity(delta)
	# Stop ground movement while attacking, but keep air momentum so aerial attacks carry through.
	if is_on_floor():
		velocity.x = 0.0


func _physics_move_locked(delta: float) -> void:
	_apply_gravity(delta)
	_handle_jump(delta)
	_try_start_attacks()


func _physics_free_control(delta: float) -> void:
	_apply_gravity(delta)
	_handle_jump(delta)
	_handle_move(delta)
	_try_start_attacks()


func _try_start_attacks() -> void:
	if Input.is_action_just_pressed(action_attack):
		attack()
	if Input.is_action_just_pressed(action_heavy_attack):
		heavy_attack()
#endregion

#region Movement helpers
func _move_input() -> float:
	return Input.get_axis(action_move_left, action_move_right)


func _apply_gravity(delta: float) -> void:
	if not is_on_floor():
		velocity += get_gravity() * delta


# Jump priority: ground or coyote jump first, then consume one air jump charge.
func _handle_jump(delta: float) -> void:
	if not is_on_floor():
		coyote_timer -= delta
	if not Input.is_action_just_pressed(action_jump):
		return

	if is_on_floor() or coyote_timer > 0.0:
		velocity.y = JUMP_VELOCITY
		coyote_timer = 0.0
		Sfx.jump(false)
	elif jump_charge_left > 0:
		velocity.y = JUMP_VELOCITY
		jump_charge_left -= 1
		Sfx.jump(true)
		var direction := _move_input()
		if direction:
			velocity.x = direction * AIR_JUMP_HORIZONTAL_SPEED
			_face(direction)


func _update_footsteps(delta: float) -> void:
	# 10.0 px/s: below this horizontal speed the character counts as standing still, so no footsteps.
	if not is_on_floor() or is_attack_active or is_winding_up or absf(velocity.x) < 10.0:
		_footstep_timer = 0.0
		return
	_footstep_timer -= delta
	if _footstep_timer <= 0.0:
		Sfx.footstep()
		_footstep_timer = FOOTSTEP_INTERVAL


func _handle_move(delta: float) -> void:
	if not is_on_floor():
		_apply_air_control(delta)
		return

	var direction := _move_input()
	if direction:
		velocity.x = direction * MOVE_SPEED
		_face(direction)
	else:
		velocity.x = 0.0


func _apply_air_control(delta: float) -> void:
	var direction := _move_input()
	if direction:
		velocity.x = move_toward(velocity.x, direction * MOVE_SPEED, AIR_CONTROL_SPEED * delta)
		_face(direction)


func _face(direction: float) -> void:
	facing_right = direction > 0.0
	_apply_facing()


func _apply_facing() -> void:
	sprite.flip_h = not facing_right
	attack_collider.position.x = _collider_offset_x if facing_right else -_collider_offset_x
	attack_sprite.position.x = _sprite_offset_x if facing_right else -_sprite_offset_x
#endregion

#region Animation
func _update_anims() -> void:
	if not is_on_floor():
		if velocity.y >= 0.0:
			_set_anim(&"fall_anim")
		else:
			_set_anim(&"jump_anim")
	elif absf(velocity.x) > 0.1:
		_set_anim(&"walk_anim")
	else:
		_set_anim(&"idle_anim")


func _set_anim(anim_name: StringName) -> void:
	if sprite.animation != anim_name:
		sprite.play(anim_name)
#endregion

#region Attack flow
func attack() -> void:
	start_attack(AttackKind.LIGHT)


func heavy_attack() -> void:
	if attack_charge < ATTACK_CHARGE_MAX:
		return
	attack_charge = maxi(0, attack_charge - HEAVY_ATTACK_CHARGE_COST)
	start_attack(AttackKind.HEAVY)


func start_attack(kind: AttackKind) -> void:
	var is_heavy := kind == AttackKind.HEAVY
	var action: StringName = action_heavy_attack if is_heavy else action_attack
	var anim: StringName = &"heavy_attack_anim" if is_heavy else &"attack_anim"

	_attack_serial += 1
	var serial := _attack_serial
	is_winding_up = true
	if is_on_floor():
		velocity.x = 0.0
	sprite.animation = anim
	sprite.frame = 0
	sprite.pause()

	await _play_startup_blink(kind)
	if serial != _attack_serial:
		return
	if not await _wait_for_release(action, serial):
		return

	is_winding_up = false
	is_attack_active = true
	_is_hovering_in_air = not is_on_floor()
	if _is_hovering_in_air:
		velocity = Vector2.ZERO
	sprite.animation = anim
	sprite.frame = 1
	sprite.pause()
	_prepare_hit(kind)
	Sfx.swing(is_heavy)
	set_attack_hitbox_active(true)

	await get_tree().create_timer(ATTACK_ACTIVE_TIME).timeout
	if serial != _attack_serial:
		return
	_end_attack_active()


# Waits while the attack button is held. Returns false if the attack was cancelled.
func _wait_for_release(action: StringName, serial: int) -> bool:
	while Input.is_action_pressed(action):
		await get_tree().process_frame
		if serial != _attack_serial:
			return false
	return true


func _prepare_hit(kind: AttackKind) -> void:
	var is_heavy := kind == AttackKind.HEAVY
	current_hit = HitData.new()
	# Per-move tuning: heavy hits deal 12 damage at 1.2x power; light hits deal 3 at 1.0x.
	current_hit.damage = 12.0 if is_heavy else 3.0
	current_hit.attack_power = 1.2 if is_heavy else 1.0
	current_hit.base_knockback = 100.0
	hit_landed = false


func _end_attack_active() -> void:
	set_attack_hitbox_active(false)
	is_attack_active = false
	_is_hovering_in_air = false


func set_attack_hitbox_active(is_active: bool) -> void:
	# Deferred because monitoring can't be changed during physics callbacks such as collision signals.
	attack_area.set_deferred("monitoring", is_active)
	attack_sprite.visible = is_active
	attack_area.set_deferred("monitorable", is_active)


# Telegraph before the hitbox goes live. Heavy attacks blink 4 times with a 0.16s ready delay;
# light attacks blink once with a 0.06s ready delay.
func _play_startup_blink(kind: AttackKind) -> void:
	var is_heavy := kind == AttackKind.HEAVY
	var blink_count := 4 if is_heavy else 1
	var ready_delay := 0.16 if is_heavy else 0.06

	for _blink in blink_count:
		sprite.modulate = Color(1.5, 1.5, 1.5, 0.75)
		await get_tree().create_timer(0.04).timeout
		sprite.modulate = Color.WHITE
		await get_tree().create_timer(0.04).timeout
	sprite.modulate = Color(2, 2, 2)
	await get_tree().create_timer(ready_delay).timeout
	sprite.modulate = Color.WHITE
	Sfx.attack_ready()
#endregion

#region Hit / death
func notify_attack_landed(victim: Node = null) -> void:
	if hit_landed or current_hit == null or victim == null or victim == self:
		return
	hit_landed = true
	set_attack_hitbox_active(false)
	# Light hits reward the attacker with an extra air jump and one attack charge toward a heavy attack.
	if current_hit.damage <= LIGHT_HIT_MAX_DAMAGE:
		jump_charge_left += 1
		attack_charge = clampi(attack_charge + 1, 0, ATTACK_CHARGE_MAX)
		_freeze_for_hitstop()
		Sfx.light_hit()
	else:
		Sfx.heavy_hit()
	if victim.has_method("hurt"):
		victim.hurt(current_hit, global_position.x, velocity.y)


func _on_hurt_box_area_entered(area: Area2D) -> void:
	var attacker := area.get_parent()
	if attacker == null or attacker == self:
		return
	if attacker.has_method("notify_attack_landed"):
		attacker.notify_attack_landed(self)


func hurt(hit: HitData, attacker_x: float, attacker_velocity_y: float) -> void:
	if hit == null:
		return
	_attack_serial += 1 # cancels any attack in progress
	is_winding_up = false
	is_attack_active = false
	_is_hovering_in_air = false
	set_attack_hitbox_active(false)
	Sfx.hurt()

	if hit.damage <= LIGHT_HIT_MAX_DAMAGE:
		_play_hit_blink()
		_freeze_for_hitstop()
		GameManager.play_light_hit_fx("light")
	elif hit.damage >= HEAVY_IMPACT_MIN_DAMAGE:
		GameManager.play_impact()
	_spawn_hit_spark()

	# attacker_velocity_y is the ATTACKER's vertical speed (passed in from notify_attack_landed),
	# not this fighter's. If the attacker is moving vertically, the hit deals 1.5x damage.
	var airborne_multiplier := 1.5 if absf(attacker_velocity_y) > 0.0 else 1.0
	total_damage += hit.damage * airborne_multiplier
	damage_changed.emit(total_damage)

	# Knockback grows with accumulated percent (total_damage ^ 1.5), scaled by the move's attack_power.
	var knockback := (hit.base_knockback + pow(total_damage, 1.5) * 0.25) * hit.attack_power
	var push_direction := signf(global_position.x - attacker_x)
	if push_direction == 0.0:
		push_direction = 1.0
	# Horizontal push is weaker (0.8) than the upward launch (1.5).
	velocity = Vector2(push_direction * knockback * 0.8, -knockback * 1.5)
	move_lock_timer = MOVE_LOCK_ON_HIT


func _spawn_hit_spark() -> void:
	var spark := HIT_SPARK.instantiate()
	spark.lifetime = LIGHT_HITSTOP * 2.0
	get_tree().current_scene.add_child(spark)
	spark.global_position = hurt_box_collision.global_position
	spark.emitting = true


func _play_hit_blink() -> void:
	for _blink in 2:
		sprite.modulate = Color(1.5, 1.5, 1.5, 0.55)
		await get_tree().create_timer(0.04).timeout
		sprite.modulate = Color.WHITE
		await get_tree().create_timer(0.04).timeout
	sprite.modulate = Color.WHITE


func die() -> void:
	total_damage = 0.0
	damage_changed.emit(total_damage)
	global_position = spawn_pos
	died.emit()
	velocity = Vector2.ZERO


func _freeze_for_hitstop() -> void:
	set_physics_process(false)
	# create_timer args: process_always=true, process_in_physics=false, ignore_time_scale=true.
	# Ignoring time scale keeps hitstop length unaffected by the impact slow-motion.
	await get_tree().create_timer(LIGHT_HITSTOP, true, false, true).timeout
	set_physics_process(true)
#endregion
