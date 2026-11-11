using Godot;

public partial class HitSpark : CpuParticles2D
{
	public void OnFinished()
	{
		QueueFree();
	}
}
