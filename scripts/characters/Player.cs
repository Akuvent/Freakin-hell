using Godot;
using System.Threading.Tasks;

public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	[Signal]
	public delegate void DamageChangedEventHandler(float amount);

	#region Movement
	public const float MoveSpeed = 150.0f;
	public const float JumpVelocity = -350.0f;
	public const float AirControlSpeed = 560.0f; // how fast mid-air velocity moves toward input, per second
	public const float AirJumpHorizontalSpeed = 150.0f;
	public const float FootstepInterval = 0.22f;

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

	public const int AttackChargeMax = 5;
	public const int HeavyAttackChargeCost = 3;
	public const float AttackActiveTime = 0.1f;
	public const float MoveLockOnHit = 0.12f;
	public const float LightHitstop = 0.1f;
	public const float LightHitMaxDamage = 8.0f; // hits at or below this are light hits
	public const float HeavyImpactMinDamage = 9.0f; // hits at or above this trigger the impact screen
	private static readonly PackedScene HitSparkScene = GD.Load<PackedScene>("res://scenes/vfx/hit_spark.tscn");

	public bool IsWindingUp = false; // attack startup, before the hitbox is live
	public bool IsAttackActive = false; // hitbox is live
	private bool _isHoveringInAir = false; // aerial attack: holds position in the air
	private int _attackSerial = 0; // bumped to cancel attack coroutines that are still waiting
	public float MoveLockTimer = 0.0f;
	public HitData CurrentHit;
	public float TotalDamage = 0.0f;
	public bool HitLanded = false;
	public int AttackCharge = 0;
	#endregion

	#region Node refs
	public AnimatedSprite2D Sprite;
	public Area2D AttackArea;
	public CollisionShape2D AttackCollider;
	public Sprite2D AttackSprite;
	public CollisionShape2D HurtBoxCollision;
	private float _colliderOffsetX = 0.0f; // distance from the body centre to the attack hitbox
	private float _spriteOffsetX = 0.0f; // distance from the body centre to the attack sprite
	private Sfx _sfx;
	private GameManager _gameManager;
	#endregion

	#region Lifecycle
	public override void _Ready()
	{
		Sprite = GetNode<AnimatedSprite2D>("CharSprite2D");
		AttackArea = GetNode<Area2D>("AttackArea");
		AttackCollider = GetNode<CollisionShape2D>("AttackArea/AttackAreaCollider");
		AttackSprite = GetNode<Sprite2D>("AttackArea/AttackAreaSprite");
		HurtBoxCollision = GetNode<CollisionShape2D>("HurtBox/HurtBoxCollision");
		_sfx = GetNode<Sfx>("/root/Sfx");
		_gameManager = GetNode<GameManager>("/root/GameManager");

		SpawnPos = GlobalPosition;
		_colliderOffsetX = Mathf.Abs(AttackCollider.Position.X);
		_spriteOffsetX = Mathf.Abs(AttackSprite.Position.X);
		SetAttackHitboxActive(false);
		ApplyFacing();
	}

	public override void _PhysicsProcess(double delta)
	{
		float deltaF = (float)delta;

		if (MoveLockTimer > 0.0f)
		{
			MoveLockTimer = Mathf.Max(0.0f, MoveLockTimer - deltaF);
		}

		if (_isHoveringInAir)
		{
			PhysicsAirAttackHover();
		}
		else if (IsWindingUp || IsAttackActive)
		{
			PhysicsWhileAttacking(deltaF);
		}
		else if (MoveLockTimer > 0.0f)
		{
			PhysicsMoveLocked(deltaF);
		}
		else
		{
			PhysicsFreeControl(deltaF);
		}

		MoveAndSlide();
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

	private void PhysicsWhileAttacking(float delta)
	{
		ApplyGravity(delta);
		// Stop ground movement while attacking, but keep air momentum so aerial attacks carry through.
		if (IsOnFloor())
		{
			Vector2 velocity = Velocity;
			velocity.X = 0.0f;
			Velocity = velocity;
		}
	}

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
			velocity.Y = JumpVelocity;
			Velocity = velocity;
			CoyoteTimer = 0.0f;
			_sfx.Jump(false);
		}
		else if (JumpChargeLeft > 0)
		{
			Vector2 velocity = Velocity;
			velocity.Y = JumpVelocity;
			Velocity = velocity;
			JumpChargeLeft -= 1;
			_sfx.Jump(true);
			float direction = MoveInput();
			if (direction != 0.0f)
			{
				velocity = Velocity;
				velocity.X = direction * AirJumpHorizontalSpeed;
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
			ApplyAirControl(delta);
			return;
		}

		float direction = MoveInput();
		Vector2 velocity = Velocity;
		if (direction != 0.0f)
		{
			velocity.X = direction * MoveSpeed;
			Velocity = velocity;
			Face(direction);
		}
		else
		{
			velocity.X = 0.0f;
			Velocity = velocity;
		}
	}

	private void ApplyAirControl(float delta)
	{
		float direction = MoveInput();
		if (direction != 0.0f)
		{
			Vector2 velocity = Velocity;
			velocity.X = Mathf.MoveToward(velocity.X, direction * MoveSpeed, AirControlSpeed * delta);
			Velocity = velocity;
			Face(direction);
		}
	}

	private void Face(float direction)
	{
		FacingRight = direction > 0.0f;
		ApplyFacing();
	}

	private void ApplyFacing()
	{
		Sprite.FlipH = !FacingRight;
		Vector2 colliderPos = AttackCollider.Position;
		colliderPos.X = FacingRight ? _colliderOffsetX : -_colliderOffsetX;
		AttackCollider.Position = colliderPos;
		Vector2 spritePos = AttackSprite.Position;
		spritePos.X = FacingRight ? _spriteOffsetX : -_spriteOffsetX;
		AttackSprite.Position = spritePos;
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
		if (Sprite.Animation != animName)
		{
			Sprite.Play(animName);
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
		if (AttackCharge < AttackChargeMax)
		{
			return;
		}
		AttackCharge = Mathf.Max(0, AttackCharge - HeavyAttackChargeCost);
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
		Sprite.Animation = anim;
		Sprite.Frame = 0;
		Sprite.Pause();

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
		Sprite.Animation = anim;
		Sprite.Frame = 1;
		Sprite.Pause();
		PrepareHit(kind);
		_sfx.Swing(isHeavy);
		SetAttackHitboxActive(true);

		await ToSignal(GetTree().CreateTimer(AttackActiveTime), SceneTreeTimer.SignalName.Timeout);
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
		// Per-move tuning: heavy hits deal 12 damage at 1.2x power; light hits deal 3 at 1.0x.
		CurrentHit.Damage = isHeavy ? 12.0f : 3.0f;
		CurrentHit.AttackPower = isHeavy ? 1.2f : 1.0f;
		CurrentHit.BaseKnockback = 100.0f;
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
		AttackArea.SetDeferred(Area2D.PropertyName.Monitoring, isActive);
		AttackSprite.Visible = isActive;
		AttackArea.SetDeferred(Area2D.PropertyName.Monitorable, isActive);
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
			Sprite.Modulate = new Color(1.5f, 1.5f, 1.5f, 0.75f);
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			Sprite.Modulate = Colors.White;
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}
		Sprite.Modulate = new Color(2.0f, 2.0f, 2.0f);
		await ToSignal(GetTree().CreateTimer(readyDelay), SceneTreeTimer.SignalName.Timeout);
		Sprite.Modulate = Colors.White;
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
		if (CurrentHit.Damage <= LightHitMaxDamage)
		{
			JumpChargeLeft += 1;
			AttackCharge = Mathf.Clamp(AttackCharge + 1, 0, AttackChargeMax);
			_ = FreezeForHitstop();
			_sfx.LightHit();
		}
		else
		{
			_sfx.HeavyHit();
		}
		if (victim is Player playerVictim)
		{
			playerVictim.Hurt(CurrentHit, GlobalPosition.X, Velocity.Y);
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

	public void Hurt(HitData hit, float attackerX, float attackerVelocityY)
	{
		if (hit == null)
		{
			return;
		}
		_attackSerial += 1; // cancels any attack in progress
		IsWindingUp = false;
		IsAttackActive = false;
		_isHoveringInAir = false;
		SetAttackHitboxActive(false);
		_sfx.Hurt();

		if (hit.Damage <= LightHitMaxDamage)
		{
			_ = PlayHitBlink();
			_ = FreezeForHitstop();
			_gameManager.PlayLightHitFx("light");
		}
		else if (hit.Damage >= HeavyImpactMinDamage)
		{
			_gameManager.PlayImpact();
		}
		SpawnHitSpark();

		// attackerVelocityY is the ATTACKER's vertical speed (passed in from NotifyAttackLanded),
		// not this fighter's. If the attacker is moving vertically, the hit deals 1.5x damage.
		float airborneMultiplier = Mathf.Abs(attackerVelocityY) > 0.0f ? 1.5f : 1.0f;
		TotalDamage += hit.Damage * airborneMultiplier;
		EmitSignal(SignalName.DamageChanged, TotalDamage);

		// Knockback grows with accumulated percent (TotalDamage ^ 1.5), scaled by the move's attack power.
		float knockback = (hit.BaseKnockback + Mathf.Pow(TotalDamage, 1.5f) * 0.25f) * hit.AttackPower;
		float pushDirection = Mathf.Sign(GlobalPosition.X - attackerX);
		if (pushDirection == 0.0f)
		{
			pushDirection = 1.0f;
		}
		// Horizontal push is weaker (0.8) than the upward launch (1.5).
		Velocity = new Vector2(pushDirection * knockback * 0.8f, -knockback * 1.5f);
		MoveLockTimer = MoveLockOnHit;
	}

	private void SpawnHitSpark()
	{
		HitSpark spark = HitSparkScene.Instantiate<HitSpark>();
		spark.Lifetime = LightHitstop * 2.0f;
		GetTree().CurrentScene.AddChild(spark);
		spark.GlobalPosition = HurtBoxCollision.GlobalPosition;
		spark.Emitting = true;
	}

	private async Task PlayHitBlink()
	{
		for (int i = 0; i < 2; i++)
		{
			Sprite.Modulate = new Color(1.5f, 1.5f, 1.5f, 0.55f);
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			Sprite.Modulate = Colors.White;
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
		}
		Sprite.Modulate = Colors.White;
	}

	public void Die()
	{
		TotalDamage = 0.0f;
		EmitSignal(SignalName.DamageChanged, TotalDamage);
		GlobalPosition = SpawnPos;
		EmitSignal(SignalName.Died);
		Velocity = Vector2.Zero;
	}

	private async Task FreezeForHitstop()
	{
		SetPhysicsProcess(false);
		// CreateTimer args: processAlways=true, processInPhysics=false, ignoreTimeScale=true.
		// Ignoring time scale keeps hitstop length unaffected by the impact slow-motion.
		await ToSignal(GetTree().CreateTimer(LightHitstop, true, false, true), SceneTreeTimer.SignalName.Timeout);
		SetPhysicsProcess(true);
	}
	#endregion
}
