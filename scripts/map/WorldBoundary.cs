using Godot;


public partial class WorldBoundary : Area2D
{
	public void OnBodyEntered(Node2D body)
	{
		if (body.HasMethod("die"))
		{
			body.Call("die");
		}
	}
}
