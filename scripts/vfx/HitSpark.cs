using Godot;

public partial class HitSpark : CPUParticles2D
{
	public void OnFinished()
	{
		QueueFree();
	}
}
