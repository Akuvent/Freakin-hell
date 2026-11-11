using Godot;
using System.Collections.Generic;

public partial class RotController : Node2D
{
	public static readonly Dictionary<string, float> FallbackSettings = new Dictionary<string, float>
	{
		{"glitch_start_seconds", 60},
		{"decision_seconds", 100},
		{"virus_chance", 0.2f},
		{"virus_damage_per_second", 2.0f},
		{"roll_interval_min_seconds", 10},
		{"roll_interval_max_seconds", 15},
	};
	public Dictionary<string, float> Settings = new Dictionary<string, float>();
	public List<Node2D> Platforms = new List<Node2D>();
	public bool GlitchStarted = false;
	public float? NextRollTime = null;
	public int DecidedCount = 0;
	public List<Tween> GlitchTweens = new List<Tween>();
	private GameManager _gameManager;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		Godot.Collections.Dictionary gameData = _gameManager.GameData;
		Godot.Collections.Dictionary section;

		if (!gameData.TryGetValue("map_rot", out Variant sectionVariant) || sectionVariant.VariantType != Variant.Type.Dictionary)
		{
			GD.PushError("RotController.cs: 'map_rot' section missing from defaults.json, using fallback values");
			section = new Godot.Collections.Dictionary();
		}
		else
		{
			section = sectionVariant.AsGodotDictionary();
		}
		foreach (string key in FallbackSettings.Keys)
		{
			if (section.ContainsKey(key))
			{
				Settings[key] = section[key].AsSingle();
			}
			else
			{
				GD.PushError($"RotController.cs: '{key}' missing from defaults.json, using fallback {FallbackSettings[key]}");
				Settings[key] = FallbackSettings[key];
			}
		}
		foreach (Node child in GetChildren())
		{
			if (child is Node2D platform)
			{
				Platforms.Add(platform);
			}
		}
		if (Platforms.Count == 0)
		{
			GD.PushError("RotController.cs: no platforms found under RotController");
		}
	}

	// The round clock lives in GameManager (GameManager.round_clock). Read it here, don't count it here.
	public override void _Process(double delta)
	{
		if (!GlitchStarted && _gameManager.RoundClock >= Settings["glitch_start_seconds"])
		{
			StartGlitch();
		}
	}

	public void StartGlitch()
	{
		if (GlitchStarted)
		{
			return;
		}
		GlitchStarted = true;
		OngoingGlitch();
	}

	public void OngoingGlitch()
	{
		foreach (Node2D platform in Platforms)
		{
			Polygon2D visual = platform.GetNode<Polygon2D>("PlatformVisual");
			Tween tween = CreateTween().SetLoops();
			tween.TweenProperty(visual, "modulate", new Color(1.0f, 0.4f, 0.9f), 0.15);
			tween.TweenProperty(visual, "modulate", Colors.White, 0.15);
			GlitchTweens.Add(tween);
		}
	}

	public void ResetRound()
	{
		GlitchStarted = false;
		NextRollTime = null;
		DecidedCount = 0;
		foreach (Node2D platform in Platforms)
		{
			// Floor nodes have no script yet, so this call fails at runtime (same as the GDScript version).
			platform.Call("reset");
		}
	}

	public void PlatformDecided(Node2D platform, object result)
	{
		DecidedCount++;
	}
}
