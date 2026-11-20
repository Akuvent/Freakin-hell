using Godot;

[GlobalClass]
public partial class HitData : Resource
{
	/// HP taken on hit.
	[Export] public float Damage = 5.0f;
	/// Set when the strike is built so FX and cards do not rely on damage thresholds.
	[Export] public bool IsHeavy = false;
}
