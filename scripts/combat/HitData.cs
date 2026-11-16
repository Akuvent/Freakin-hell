using Godot;

[GlobalClass]
public partial class HitData : Resource
{
	/// HP taken on hit.
	[Export] public float Damage = 5.0f;
}
