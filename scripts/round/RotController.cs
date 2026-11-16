using Godot;
using System;
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

	// How far a gone platform falls, and how long the fall takes. Visual only, not in defaults.json.
	private const float GoneFallDistance = 800.0f;
	private const float GoneFallSeconds = 2.5f;
	// Bright green so a virus platform is easy to tell apart from the others.
	private static readonly Color VirusColor = new Color(0.2f, 1.0f, 0.3f);
	// Player checks this group to know it is standing on a virus platform.
	public const string VirusGroup = "virus_platform";

	public Dictionary<string, float> Settings = new Dictionary<string, float>();
	public List<Node2D> Platforms = new List<Node2D>();
	public bool GlitchStarted = false;
	private readonly List<Node2D> _undecided = new List<Node2D>();
	private readonly Dictionary<Node2D, Tween> _flickerTweens = new Dictionary<Node2D, Tween>();
	private readonly Random _random = new Random();
	private float _nextRollTime;
	private GameManager _gameManager;
	private MatchManager _matchManager;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_gameManager = GetNode<GameManager>("/root/GameManager");
		_matchManager = GetNode<MatchManager>("/root/MatchManager");
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

		_undecided.AddRange(Platforms);
		_nextRollTime = Settings["decision_seconds"];
	}

	// The round clock lives in MatchManager. Read it here, don't count it here.
	public override void _Process(double delta)
	{
		float clock = _matchManager.RoundClock;
		if (!GlitchStarted && clock >= Settings["glitch_start_seconds"])
		{
			StartGlitch();
		}
		if (GlitchStarted && _undecided.Count > 0 && clock >= _nextRollTime)
		{
			RollPlatform(clock);
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
			_flickerTweens[platform] = tween;
		}
	}

	// Picks one platform that hasn't rolled yet, decides gone or virus, and schedules the next roll.
	private void RollPlatform(float clock)
	{
		int index = _random.Next(_undecided.Count);
		Node2D platform = _undecided[index];
		_undecided.RemoveAt(index);

		if (_random.NextDouble() < Settings["virus_chance"])
		{
			MakeVirus(platform);
		}
		else
		{
			MakeGone(platform);
		}

		float minGap = Settings["roll_interval_min_seconds"];
		float maxGap = Settings["roll_interval_max_seconds"];
		_nextRollTime = clock + minGap + (float)_random.NextDouble() * (maxGap - minGap);
	}

	// Gone: the platform stops flickering, loses its collision, and falls away while fading out.
	private void MakeGone(Node2D platform)
	{
		StopFlicker(platform);
		Polygon2D visual = platform.GetNode<Polygon2D>("PlatformVisual");
		visual.Modulate = Colors.White;

		CollisionShape2D collision = platform.GetNode<CollisionShape2D>("CollisionShape2D");
		collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		Vector2 fallTarget = platform.Position + new Vector2(0.0f, GoneFallDistance);
		Tween fall = CreateTween();
		fall.TweenProperty(platform, "position", fallTarget, GoneFallSeconds);
		fall.Parallel().TweenProperty(visual, "modulate:a", 0.0f, GoneFallSeconds);
	}

	// Virus: the platform stays solid, gets a distinct color, and is tagged so fighters on it take damage.
	private void MakeVirus(Node2D platform)
	{
		StopFlicker(platform);
		Polygon2D visual = platform.GetNode<Polygon2D>("PlatformVisual");
		visual.Modulate = Colors.White;
		visual.Color = VirusColor;
		platform.AddToGroup(VirusGroup);
	}

	private void StopFlicker(Node2D platform)
	{
		if (_flickerTweens.TryGetValue(platform, out Tween tween))
		{
			tween.Kill();
			_flickerTweens.Remove(platform);
		}
	}
}
