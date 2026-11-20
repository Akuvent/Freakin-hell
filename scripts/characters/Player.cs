using Godot;
using System.Threading.Tasks;

public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	[Signal]
	public delegate void HpChangedEventHandler(float hp);

	#region Movement
	public const float FootstepInterval = 0.22f;

	[Export] public string CharacterId = "default";
	public CharacterStats Stats = new CharacterStats();
	[Export] public int JumpCharge = 1; // air jumps restored on landing
	[Export] public float CoyoteTime = 0.1f;
	[Export] public bool FacingRight = true;
	[Export] public StringName ActionMoveLeft = "p1_move_left";
	[Export] public StringName ActionMoveRight = "p1_move_right";
	[Export] public StringName ActionJump = "p1_jump";
	[Export] public StringName ActionAttack = "p1_attack";
	[Export] public StringName ActionHeavyAttack = "p1_heavy_attack";

	public int JumpChargeLeft = 0;
	public float CoyoteTimer = 0.0f;
	private float _footstepTimer = 0.0f;
	public Vector2 SpawnPos;
	#endregion

	#region Combat state
	public enum AttackKind
	{
		Light,
		Heavy,
	}

	private PackedScene _hitSparkScene;

	public bool IsWindingUp = false; // attack startup, before the hitbox is live
	public bool IsAttackActive = false; // hitbox is live
	private bool _isHoveringInAir = false; // aerial attack: holds position in the air
	private int _attackSerial = 0; // bumped to cancel attack coroutines that are still waiting
	public HitData CurrentHit;
	public float CurrentHp = 100.0f;
	public float VirusDamagePerSecond = 2.0f; // overwritten from defaults.json in _Ready
	public bool HitLanded = false;
	public int AttackCharge = 0;
	// Brief post-hit lock: no ground snap / drift so launch velocity survives (platformer_fighter pattern).
	private float _moveLockTimer = 0.0f;
	#endregion

	#region Node refs
	private AnimatedSprite2D _sprite;
	private Area2D _attackArea;
	private CollisionShape2D _attackCollider;
	private Sprite2D _attackSprite;
	private CollisionShape2D _hurtBoxCollision;
	private float _colliderOffsetX = 0.0f; // distance from the body centre to the attack hitbox
	private float _spriteOffsetX = 0.0f; // distance from the body centre to the attack sprite
	private Sfx _sfx;
	private GameManager _gameManager;
	private MatchManager _matchManager;
	#endregion

	#region Lifecycle
	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("CharSprite2D");
		_attackArea = GetNode<Area2D>("AttackArea");
		_attackCollider = GetNode<CollisionShape2D>("AttackArea/AttackAreaCollider");
		_attackSprite = GetNode<Sprite2D>("AttackArea/AttackAreaSprite");
		_hurtBoxCollision = GetNode<CollisionShape2D>("HurtBox/HurtBoxCollision");
		_sfx = GetNode<Sfx>("/root/Sfx");
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_matchManager = GetNode<MatchManager>("/root/MatchManager");
		_hitSparkScene = GD.Load<PackedScene>("res://scenes/vfx/hit_spark.tscn");
		Stats = _gameManager.GetCharacterStats(CharacterId);
		CurrentHp = Stats.MaxHp;
		LoadVirusDamage();

		SpawnPos = GlobalPosition;
		_colliderOffsetX = Mathf.Abs(_attackCollider.Position.X);
		_spriteOffsetX = Mathf.Abs(_attackSprite.Position.X);
		SetAttackHitboxActive(false);
		ApplyFacing();
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaF = (float)delta;
		TickMoveLock(deltaF);

		if (_isHoveringInAir)
		{
			PhysicsAirAttackHover();
		}
		else if (IsWindingUp || IsAttackActive)
		{
			PhysicsWhileAttacking(deltaF);
		}
		else if (_moveLockTimer > 0.0f)
		{
			PhysicsMoveLocked(deltaF);
		}
		else
		{
			PhysicsFreeControl(deltaF);
		}

		MoveAndSlide();
		ApplyVirusDamage(deltaF);
		UpdateFootsteps(deltaF);

		// During wind-up, start_attack() holds the attack animation; don't let locomotion animations override it.
		if (!IsWindingUp || IsAttackActive)
		{
			UpdateAnims();
		}

		if (IsOnFloor())
		{
			CoyoteTimer = CoyoteTime;
			JumpChargeLeft = JumpCharge;
		}
	}
	#endregion

	#region Physics branches
	private void PhysicsAirAttackHover()
	{
		Velocity = Vector2.Zero;
	}

	private void TickMoveLock(float delta)
	{
		if (_moveLockTimer > 0.0f)
		{
			_moveLockTimer = Mathf.Max(0.0f, _moveLockTimer - delta);
		}
	}

	private void PhysicsWhileAttacking(float delta)
	{
		ApplyGravity(delta);
		// Ground plant only; aerial swings must not erase knockback X.
		if (IsOnFloor())
		{
			Vector2 velocity = Velocity;
			velocity.X = 0.0f;
			Velocity = velocity;
		}
	}

	// Hit stun window: physics still runs, but locomotion must not overwrite launch velocity.
	private void PhysicsMoveLocked(float delta)
	{
		ApplyGravity(delta);
		HandleJump(delta);
		TryStartAttacks();
	}

	private void PhysicsFreeControl(float delta)
	{
		ApplyGravity(delta);
		HandleJump(delta);
		HandleMove(delta);
		TryStartAttacks();
	}

	private void TryStartAttacks()
	{
		if (Input.IsActionJustPressed(ActionAttack))
		{
			Attack();
		}
		if (Input.IsActionJustPressed(ActionHeavyAttack))
		{
			HeavyAttack();
		}
	}
	#endregion

	#region Movement helpers
	private float MoveInput()
	{
		return Input.GetAxis(ActionMoveLeft, ActionMoveRight);
	}

	private void ApplyGravity(float delta)
	{
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * delta;
		}
	}

	// Jump priority: ground or coyote jump first, then consume one air jump charge.
	private void HandleJump(float delta)
	{
		if (!IsOnFloor())
		{
			CoyoteTimer -= delta;
		}
		if (!Input.IsActionJustPressed(ActionJump))
		{
			return;
		}

		if (IsOnFloor() || CoyoteTimer > 0.0f)
		{
			Vector2 velocity = Velocity;
			velocity.Y = Stats.JumpVelocity;
			Velocity = velocity;
			CoyoteTimer = 0.0f;
			_sfx.Jump(false);
		}
		else if (JumpChargeLeft > 0)
		{
			Vector2 velocity = Velocity;
			velocity.Y = Stats.JumpVelocity;
			Velocity = velocity;
			JumpChargeLeft -= 1;
			_sfx.Jump(true);
			float direction = MoveInput();
			if (direction != 0.0f)
			{
				velocity = Velocity;
				velocity.X = direction * Stats.AirJumpHorizontalSpeed;
				Velocity = velocity;
				Face(direction);
			}
		}
	}

	private void UpdateFootsteps(float delta)
	{
		// 10.0 px/s: below this horizontal speed the character counts as standing still, so no footsteps.
		if (!IsOnFloor() || IsAttackActive || IsWindingUp || Mathf.Abs(Velocity.X) < 10.0f)
		{
			_footstepTimer = 0.0f;
			return;
		}
		_footstepTimer -= delta;
		if (_footstepTimer <= 0.0f)
		{
			_sfx.Footstep();
			_footstepTimer = FootstepInterval;
		}
	}

	private void HandleMove(float delta)
	{
		if (!IsOnFloor())
		{
			ApplyAirDrift(delta);
			return;
		}

		float direction = MoveInput();
		Vector2 velocity = Velocity;
		if (direction != 0.0f)
		{
			velocity.X = direction * Stats.MoveSpeed;
			Velocity = velocity;
			Face(direction);
		}
		else
		{
			velocity.X = 0.0f;
			Velocity = velocity;
		}
	}

	// Weak steer in air: fights knockback without snapping to walk speed.
	private void ApplyAirDrift(float delta)
	{
		float direction = MoveInput();
		if (direction == 0.0f)
		{
			return;
		}
		Vector2 velocity = Velocity;
		velocity.X = Mathf.MoveToward(velocity.X, direction * Stats.MoveSpeed, Stats.AirDriftAccel * delta);
		Velocity = velocity;
		Face(direction);
	}

	private void Face(float direction)
	{
		FacingRight = direction > 0.0f;
		ApplyFacing();
	}

	private void ApplyFacing()
	{
		_sprite.FlipH = !FacingRight;
		Vector2 colliderPos = _attackCollider.Position;
		colliderPos.X = FacingRight ? _colliderOffsetX : -_colliderOffsetX;
		_attackCollider.Position = colliderPos;
		Vector2 spritePos = _attackSprite.Position;
		spritePos.X = FacingRight ? _spriteOffsetX : -_spriteOffsetX;
		_attackSprite.Position = spritePos;
	}
	#endregion

	#region Animation
	private void UpdateAnims()
	{
		if (!IsOnFloor())
		{
			if (Velocity.Y >= 0.0f)
			{
				SetAnim("fall_anim");
			}
			else
			{
				SetAnim("jump_anim");
			}
		}
		else if (Mathf.Abs(Velocity.X) > 0.1f)
		{
			SetAnim("walk_anim");
		}
		else
		{
			SetAnim("idle_anim");
		}
	}

	private void SetAnim(StringName animName)
	{
		if (_sprite.Animation != animName)
		{
			_sprite.Play(animName);
		}
	}
	#endregion

	#region Attack flow
	public void Attack()
	{
		_ = StartAttack(AttackKind.Light);
	}

	public void HeavyAttack()
	{
		if (AttackCharge < Stats.AttackChargeMax)
		{
			return;
		}
		AttackCharge = Mathf.Max(0, AttackCharge - Stats.HeavyAttackChargeCost);
		_ = StartAttack(AttackKind.Heavy);
	}

	private async Task StartAttack(AttackKind kind)
	{
		bool isHeavy = kind == AttackKind.Heavy;
		StringName action = isHeavy ? ActionHeavyAttack : ActionAttack;
		StringName anim = isHeavy ? "heavy_attack_anim" : "attack_anim";

		_attackSerial += 1;
		int serial = _attackSerial;
		IsWindingUp = true;
		if (IsOnFloor())
		{
			Vector2 velocity = Velocity;
			velocity.X = 0.0f;
			Velocity = velocity;
		}
		_sprite.Animation = anim;
		_sprite.Frame = 0;
		_sprite.Pause();

		await PlayStartupBlink(kind);
		if (serial != _attackSerial)
		{
			return;
		}
		if (!await WaitForRelease(action, serial))
		{
			return;
		}

		IsWindingUp = false;
		IsAttackActive = true;
		_isHoveringInAir = !IsOnFloor();
		if (_isHoveringInAir)
		{
			Velocity = Vector2.Zero;
		}
		_sprite.Animation = anim;
		_sprite.Frame = 1;
		_sprite.Pause();
		PrepareHit(kind);
		_sfx.Swing(isHeavy);
		SetAttackHitboxActive(true);

		await ToSignal(GetTree().CreateTimer(Stats.AttackActiveTime), SceneTreeTimer.SignalName.Timeout);
		if (serial != _attackSerial)
		{
			return;
		}
		EndAttackActive();
	}

	// Waits while the attack button is held. Returns false if the attack was cancelled.
	private async Task<bool> WaitForRelease(StringName action, int serial)
	{
		while (Input.IsActionPressed(action))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (serial != _attackSerial)
			{
				return false;
			}
		}
		return true;
	}

	private void PrepareHit(AttackKind kind)
	{
		bool isHeavy = kind == AttackKind.Heavy;
		CurrentHit = new HitData();
		// Per-move tuning: heavy hits take 12 HP; light hits take 3 HP.
		CurrentHit.Damage = isHeavy ? Stats.HeavyAttackDamage : Stats.LightAttackDamage;
		HitLanded = false;
	}

	private void EndAttackActive()
	{
		SetAttackHitboxActive(false);
		IsAttackActive = false;
		_isHoveringInAir = false;
	}

	public void SetAttackHitboxActive(bool isActive)
	{
		// Deferred because monitoring can't be changed during physics callbacks such as collision signals.
		_attackArea.SetDeferred(Area2D.PropertyName.Monitoring, isActive);
		_attackSprite.Visible = isActive;
		_attackArea.SetDeferred(Area2D.PropertyName.Monitorable, isActive);
	}

	// Telegraph before the hitbox goes live. Heavy attacks blink 4 times with a 0.16s ready delay;
	// light attacks blink once with a 0.06s ready delay.
	private async Task PlayStartupBlink(AttackKind kind)
	{
		bool isHeavy = kind == AttackKind.Heavy;
		int blinkCount = isHeavy ? 4 : 1;
		float readyDelay = isHeavy ? 0.16f : 0.06f;

		for (int i = 0; i < blinkCount; i++)
		{
			_sprite.Modulate = new Color(1.5f, 1.5f, 1.5f, 0.75f);
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			_sprite.Modulate = Colors.White;
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}
		_sprite.Modulate = new Color(2.0f, 2.0f, 2.0f);
		await ToSignal(GetTree().CreateTimer(readyDelay), SceneTreeTimer.SignalName.Timeout);
		_sprite.Modulate = Colors.White;
		_sfx.AttackReady();
	}
	#endregion

	#region Hit / death
	public void NotifyAttackLanded(Node victim = null)
	{
		if (HitLanded || CurrentHit == null || victim == null || victim == this)
		{
			return;
		}
		HitLanded = true;
		SetAttackHitboxActive(false);
		// Light hits reward the attacker with an extra air jump and one attack charge toward a heavy attack.
		if (CurrentHit.Damage <= Stats.LightMaxDamage)
		{
			JumpChargeLeft += 1;
			AttackCharge = Mathf.Clamp(AttackCharge + 1, 0, Stats.AttackChargeMax);
			_ = FreezeForHitstop();
			_sfx.LightHit();
		}
		else
		{
			_sfx.HeavyHit();
		}
		if (victim is Player playerVictim)
		{
			playerVictim.Hurt(CurrentHit, Velocity.Y, this);
		}
	}

	public void OnHurtBoxAreaEntered(Area2D area)
	{
		Node attacker = area.GetParent();
		if (attacker == null || attacker == this)
		{
			return;
		}
		if (attacker is Player playerAttacker)
		{
			playerAttacker.NotifyAttackLanded(this);
		}
	}

	public void Hurt(HitData hit, float attackerVelocityY, Player attacker)
	{
		if (hit == null || attacker == null)
		{
			return;
		}
		_attackSerial += 1; // cancels any attack in progress
		IsWindingUp = false;
		IsAttackActive = false;
		_isHoveringInAir = false;
		SetAttackHitboxActive(false);
		_sfx.Hurt();

		if (hit.Damage <= attacker.Stats.LightMaxDamage)
		{
			_ = PlayHitBlink();
			_ = FreezeForHitstop();
			_matchManager.PlayLightHitFx("light");
		}
		else if (hit.Damage >= attacker.Stats.HeavyImpactMinDamage)
		{
			_matchManager.PlayImpact();
		}
		SpawnHitSpark();

		// attackerVelocityY is the ATTACKER's vertical speed (passed in from NotifyAttackLanded),
		// not this fighter's. If the attacker is moving vertically, the hit deals 1.5x damage.
		float airborneMultiplier = Mathf.Abs(attackerVelocityY) > 0.0f ? 1.5f : 1.0f;
		float force = hit.Damage * attacker.Stats.KnockbackPerDamage * airborneMultiplier;
		float dir = GetKnockbackDirection(attacker);
		Velocity = new Vector2(dir * force, -force * attacker.Stats.KnockbackVerticalRatio);
		_moveLockTimer = Stats.HitMoveLockSeconds;
		TakeDamage(hit.Damage * airborneMultiplier);
	}

	private float GetKnockbackDirection(Player attacker)
	{
		float dir = Mathf.Sign(GlobalPosition.X - attacker.GlobalPosition.X);
		if (dir == 0.0f)
		{
			dir = attacker.FacingRight ? 1.0f : -1f;
		}
		return dir;
	}

	// Every HP loss goes through here (hits and virus). Reaching 0 HP calls Die(), which is the one place that emits Died.
	public void TakeDamage(float amount)
	{
		CurrentHp = Mathf.Max(0.0f, CurrentHp - amount);
		EmitSignal(SignalName.HpChanged, CurrentHp);
		if (CurrentHp <= 0.0f)
		{
			Die();
		}
	}

	private void SpawnHitSpark()
	{
		HitSpark spark = _hitSparkScene.Instantiate<HitSpark>();
		spark.Lifetime = Stats.LightHitstop * 2.0f;
		GetTree().CurrentScene.AddChild(spark);
		spark.GlobalPosition = _hurtBoxCollision.GlobalPosition;
		spark.Emitting = true;
	}

	private async Task PlayHitBlink()
	{
		for (int i = 0; i < 2; i++)
		{
			_sprite.Modulate = new Color(1.5f, 1.5f, 1.5f, 0.55f);
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			_sprite.Modulate = Colors.White;
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}
		_sprite.Modulate = Colors.White;
	}

	// Reads virus_damage_per_second from the map_rot section of defaults.json. RotController logs missing keys, so this stays quiet.
	private void LoadVirusDamage()
	{
		Godot.Collections.Dictionary gameData = _gameManager.GameData;
		if (gameData.TryGetValue("map_rot", out Variant mapRot) && mapRot.VariantType == Variant.Type.Dictionary)
		{
			Godot.Collections.Dictionary mapRotSection = mapRot.AsGodotDictionary();
			if (mapRotSection.ContainsKey("virus_damage_per_second"))
			{
				VirusDamagePerSecond = mapRotSection["virus_damage_per_second"].AsSingle();
			}
		}
	}

	// True if one of the contacts from the last MoveAndSlide is a floor (normal pointing up) in the virus group.
	private bool IsStandingOnVirus()
	{
		if (!IsOnFloor())
		{
			return false;
		}
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D contact = GetSlideCollision(i);
			if (contact.GetNormal().Y > -0.7f)
			{
				continue; // not a floor contact
			}
			if (contact.GetCollider() is Node body && body.IsInGroup(RotController.VirusGroup))
			{
				return true;
			}
		}
		return false;
	}

	// Damage only while standing on a virus platform. Uses the floor contact from MoveAndSlide, not an Area2D.
	private void ApplyVirusDamage(float delta)
	{
		if (!IsStandingOnVirus())
		{
			return;
		}
		TakeDamage(VirusDamagePerSecond * delta);
	}

	public void Die()
	{
		CurrentHp = Stats.MaxHp;
		EmitSignal(SignalName.HpChanged, CurrentHp);
		GlobalPosition = SpawnPos;
		EmitSignal(SignalName.Died);
		Velocity = Vector2.Zero;
		_moveLockTimer = 0.0f;
	}

	private async Task FreezeForHitstop()
	{
		SetPhysicsProcess(false);
		// CreateTimer args: processAlways=true, processInPhysics=false, ignoreTimeScale=true.
		// Ignoring time scale keeps hitstop length unaffected by the impact slow-motion.
		await ToSignal(GetTree().CreateTimer(Stats.LightHitstop, true, false, true), SceneTreeTimer.SignalName.Timeout);
		SetPhysicsProcess(true);
	}
	#endregion
}
