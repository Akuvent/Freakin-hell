using Godot;

[GlobalClass]
public partial class HitData : Resource
{
	/// Percent added on hit.
	[Export] public float Damage = 5.0f;
	/// Launch multiplier for this move (1.0 = normal).
	[Export] public float AttackPower = 1.0f;
	/// Shove at 0% before percent scaling.
	[Export] public float BaseKnockback = 100.0f;
}
