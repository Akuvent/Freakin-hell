using Godot;

/// Stats for one fighter id in data/characters/fighters.json.
public sealed class CharacterStats
{
	public string Id = "";
	public float MaxHp = 100.0f;
	public float MoveSpeed = 150.0f;
	public float JumpVelocity = -350.0f;
	public float AirControlSpeed = 560.0f;
	public float AirDriftAccel = 280.0f;
	public float AirJumpHorizontalSpeed = 150.0f;
	public float HitMoveLockSeconds = 0.12f;
	public int AttackChargeMax = 5;
	public int HeavyAttackChargeCost = 3;
	public float AttackActiveTime = 0.1f;
	public float LightHitstop = 0.1f;
	public float LightMaxDamage = 8.0f;
	public float HeavyImpactMinDamage = 9.0f;
	public float KnockbackPerDamage = 35.0f;
	public float KnockbackVerticalRatio = 0.55f;
	public float LightAttackDamage = 3.0f;
	public float HeavyAttackDamage = 12.0f;

	public static CharacterStats FromDictionary(string id, Godot.Collections.Dictionary def)
	{
		CharacterStats stats = new CharacterStats { Id = id };
		stats.MaxHp = ReadFloat(def, "max_hp", stats.MaxHp);
		stats.MoveSpeed = ReadFloat(def, "move_speed", stats.MoveSpeed);
		stats.JumpVelocity = ReadFloat(def, "jump_velocity", stats.JumpVelocity);
		stats.AirControlSpeed = ReadFloat(def, "air_control_speed", stats.AirControlSpeed);
		stats.AirDriftAccel = ReadFloat(def, "air_drift_accel", stats.AirDriftAccel);
		stats.AirJumpHorizontalSpeed = ReadFloat(def, "air_jump_horizontal_speed", stats.AirJumpHorizontalSpeed);
		stats.HitMoveLockSeconds = ReadFloat(def, "hit_move_lock_seconds", stats.HitMoveLockSeconds);
		stats.AttackChargeMax = ReadInt(def, "attack_charge_max", stats.AttackChargeMax);
		stats.HeavyAttackChargeCost = ReadInt(def, "heavy_attack_charge_cost", stats.HeavyAttackChargeCost);
		stats.AttackActiveTime = ReadFloat(def, "attack_active_time", stats.AttackActiveTime);
		stats.LightHitstop = ReadFloat(def, "light_hitstop", stats.LightHitstop);
		stats.LightMaxDamage = ReadFloat(def, "light_max_damage", stats.LightMaxDamage);
		stats.HeavyImpactMinDamage = ReadFloat(def, "heavy_impact_min_damage", stats.HeavyImpactMinDamage);
		stats.KnockbackPerDamage = ReadFloat(def, "knockback_per_damage", stats.KnockbackPerDamage);
		stats.KnockbackVerticalRatio = ReadFloat(def, "knockback_vertical_ratio", stats.KnockbackVerticalRatio);

		if (def.TryGetValue("attacks", out Variant attacksVariant) && attacksVariant.VariantType == Variant.Type.Dictionary)
		{
			Godot.Collections.Dictionary attacks = attacksVariant.AsGodotDictionary();
			if (attacks.TryGetValue("light", out Variant lightVariant) && lightVariant.VariantType == Variant.Type.Dictionary)
			{
				stats.LightAttackDamage = ReadFloat(lightVariant.AsGodotDictionary(), "damage", stats.LightAttackDamage);
			}
			if (attacks.TryGetValue("heavy", out Variant heavyVariant) && heavyVariant.VariantType == Variant.Type.Dictionary)
			{
				stats.HeavyAttackDamage = ReadFloat(heavyVariant.AsGodotDictionary(), "damage", stats.HeavyAttackDamage);
			}
		}

		return stats;
	}

	private static float ReadFloat(Godot.Collections.Dictionary dict, string key, float fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}
		return dict[key].AsSingle();
	}

	private static int ReadInt(Godot.Collections.Dictionary dict, string key, int fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}
		return (int)dict[key];
	}
}