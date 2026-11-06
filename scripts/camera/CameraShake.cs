using Godot;



public partial class CameraShake : Camera2D
{
	public float Trauma = 0.0f;
	public float ElapsedTime = 0.0f;
	public float Strength = 0.0f;
	// Kind of the most recent hit ("light" or "heavy"). Strength uses it until the next AddTrauma() call.
	public string AttackType { get; private set; } = string.Empty;

	public void AddTrauma(float amount, string kind)
	{
		Trauma = Mathf.Clamp(Trauma + amount, 0.0f, 1.0f);
		AttackType = kind;
	}

	public override void _Process(double delta)
	{
		if (AttackType == "light")
		{
			Strength = (Trauma * Trauma) * 10.0f;
		}
		else if (AttackType == "heavy")
		{
			Strength = (Trauma * Trauma) * 100.0f;
		}
		else
		{
			Strength = (Trauma * Trauma) * 10.0f;
		}
		ElapsedTime += (float)delta;
		// Different frequencies on x and y (35 vs 20) make the shake look less like a circle.
		Offset = new Vector2((Mathf.Sin(ElapsedTime * 35.0f) * Strength), (Mathf.Sin(ElapsedTime * 20.0f) * Strength));

		Trauma = Mathf.Max(Trauma - 1.2f * (float)delta, 0.0f);
	}
}
