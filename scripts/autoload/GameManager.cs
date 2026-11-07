using Godot;
using System;
using System.Threading.Tasks;

public partial class GameManager : Node
{
	public const int ImpactInvertFrames = 5;
	public const int ImpactBlackoutFrames = 5;
	public const float ImpactTimeScale = 0.05f;

	public Label P1StocksLabel;
	public Label P2StocksLabel;
	public Label P1PercentLabel;
	public Label P2PercentLabel;
	public int P1Stocks = 0;
	public int P2Stocks = 0;
	private bool _impactPlaying = false;
	// True while a round result is being resolved. Stops a second death in the same frame from resolving it twice.
	private bool _roundResolving = false;
	// Parsed content of res://data/tuning/defaults.json, loaded in _ready().
	public Godot.Collections.Dictionary GameData = new Godot.Collections.Dictionary();
	// Round wins needed to win the match. Set from defaults.json by apply_game_data().
	public int RoundsToWin;
	// Round wins this match. The match ends when one side reaches rounds_to_win.
	public int P1RoundsWon = 0;
	public int P2RoundsWon = 0;
	// Seconds since the current round started. Only advances while a round is running.
	public float RoundClock = 0.0f;
	private bool _roundClockRunning = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Deferred: the match scene's nodes may not exist yet when this autoload is ready.
		CallDeferred("BindMatchUI");
		GameData = LoadGameData();
	}

	public void BindMatchUI()
	{
		Node scene = GetTree().CurrentScene;
		if (scene == null)
		{
			return;
		}
		P1PercentLabel = scene.GetNodeOrNull("UI/P1_Percent") as Label;
		P2PercentLabel = scene.GetNodeOrNull("UI/P2_Percent") as Label;
		P1StocksLabel = scene.GetNodeOrNull("UI/P1_Stocks") as Label;
		P2StocksLabel = scene.GetNodeOrNull("UI/P2_Stocks") as Label;
		ApplyGameData();
		RefreshStockLabels();
		StartRoundClock();

		Player p1 = scene.GetNodeOrNull<Player>("PlayerBody1");
		Player p2 = scene.GetNodeOrNull<Player>("PlayerBody2");
		if (p1 != null && !p1.Died.IsConnected(OnP1Died))
		{
			p1.Died.Connect(OnP1Died);
		}
		if (p2 != null && !p2.Died.IsConnected(OnP2Died))
		{
			p2.Died.Connect(OnP2Died);
		}
		if (p1 != null && !p1.DamageChanged.IsConnected(OnP1Damaged))
		{
			p1.DamageChanged.Connect(OnP1Damaged);
		}
		if (p2 != null && !p2.DamageChanged.IsConnected(OnP2Damaged))
		{
			p2.DamageChanged.Connect(OnP2Damaged);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_roundClockRunning)
		{
			RoundClock += (float)delta;
		}
	}

	public void StartRoundClock()
	{
		RoundClock = 0.0f;
		_roundClockRunning = true;
	}

	public void RefreshStockLabels()
	{
		if (P1StocksLabel != null)
		{
			P1StocksLabel.Text = $"×{P1Stocks}";
		}
		if (P2StocksLabel != null)
		{
			P2StocksLabel.Text = $"×{P2Stocks}";
		}
	}

	public void OnP1Damaged(float amount)
	{
		SetPercentLabel(P1PercentLabel, amount);
	}

	public void OnP2Damaged(float amount)
	{
		SetPercentLabel(P2PercentLabel, amount);
	}

	public void SetPercentLabel(Label label, float amount)
	{
		if (label == null)
		{
			return;
		}
		// Color ramps white -> yellow -> red as damage goes from 0 to 100.
		float damageFraction = Mathf.Clamp(amount / 100.0f, 0.0f, 1.0f);
		Color color;
		if (damageFraction < 0.5f)
		{
			color = Colors.White.Lerp(Colors.Yellow, damageFraction * 2.0f);
		}
		else
		{
			color = Colors.Yellow.Lerp(Colors.Red, (damageFraction - 0.5f) * 2.0f);
		}
		// amount is stored as damage starting at 0. The HUD adds 100, so it shows 100% at zero damage.
		label.Text = $"{Mathf.RoundToInt(amount + 100.0f)}%";
		if (label.LabelSettings != null)
		{
			label.LabelSettings.FontColor = color;
		}
	}

	public void OnP1Died()
	{
		P1Stocks = Math.Max(P1Stocks - 1, 0);
		RefreshStockLabels();
		if (P1Stocks <= 0)
		{
			ResolveRoundSoon();
		}
	}

	public void OnP2Died()
	{
		P2Stocks = Math.Max(P2Stocks - 1, 0);
		RefreshStockLabels();
		if (P2Stocks <= 0)
		{
			ResolveRoundSoon();
		}
	}

	// Waits one frame so that a double KO is seen as both stocks at zero.
	public async void ResolveRoundSoon()
	{
		if (_roundResolving)
		{
			return;
		}
		_roundResolving = true;
		// Stop the clock as soon as the round is decided, so the result frame doesn't count.
		_roundClockRunning = false;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_roundResolving = false;

		bool matchOver = false;
		if (P1Stocks <= 0 && P2Stocks <= 0)
		{
			GD.Print("Draw: no round win");
		}
		else if (P1Stocks <= 0)
		{
			P2RoundsWon++;
			if (P2RoundsWon == RoundsToWin)
			{
				GD.Print("Match over: P2 wins");
				matchOver = true;
			}
		}
		else if (P2Stocks <= 0)
		{
			P1RoundsWon++;
			if (P1RoundsWon == RoundsToWin)
			{
				GD.Print("Match over: P1 wins");
				matchOver = true;
			}
		}

		if (matchOver)
		{
			EndMatch();
		}
		else
		{
			await ReloadMatch();
		}
	}

	// Called when a side wins the match. Pauses the whole tree. Nothing in this file unpauses it,
	// so the game stays paused after the match ends.
	public void EndMatch()
	{
		GetTree().Paused = true;
	}

	public async Task ReloadMatch()
	{
		SceneTree tree = GetTree();
		string path = tree.CurrentScene.SceneFilePath;
		if (string.IsNullOrEmpty(path))
		{
			GD.PushError("Error: no scene path to reload");
			return;
		}
		tree.ChangeSceneToFile(path);

		// Wait up to 10 frames for the reloaded scene to expose its UI before binding again.
		for (int i = 0; i < 10; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			if (tree.CurrentScene != null && tree.CurrentScene.GetNodeOrNull("UI/P1_Percent") != null && tree.CurrentScene.GetNodeOrNull("UI/P2_Percent") != null)
			{
				BindMatchUI();
				return;
			}
		}
		GD.PushError("Failed to reload match: no UI found");
	}

	public void PlayLightHitFx(string kind)
	{
		Node scene = GetTree().CurrentScene;
		CameraShake camera = scene.GetNodeOrNull<CameraShake>("Camera2D");
		if (camera != null)
		{
			camera.AddTrauma(0.4f, kind);
		}
	}

	public async void PlayImpact()
	{
		if (_impactPlaying)
		{
			return;
		}
		Node scene = GetTree().CurrentScene;
		if (scene == null)
		{
			return;
		}
		CanvasItem rect = scene.GetNodeOrNull("VFX/ImpactRect") as CanvasItem;
		if (rect == null)
		{
			return;
		}

		_impactPlaying = true;
		ShaderMaterial material = rect.Material as ShaderMaterial;
		CameraShake camera = scene.GetNodeOrNull<CameraShake>("Camera2D");
		Vector2 baseZoom = Vector2.One;
		if (camera != null)
		{
			baseZoom = camera.Zoom;
			// Punch in to about 2x zoom for the impact. The original zoom is restored at the end.
			camera.Zoom = baseZoom * 2.06f;
			camera.AddTrauma(0.8f, "heavy");
		}
		// Slow motion: the game runs at IMPACT_TIME_SCALE (5%) during the impact. Restored at the end.
		Engine.TimeScale = ImpactTimeScale;
		if (material != null)
		{
			material.SetShaderParameter("style", 1);
		}
		SetRectAlpha(rect, 0.0f);
		rect.Visible = true;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		SetRectAlpha(rect, 1.0f);
		await WaitFrames(ImpactInvertFrames);

		if (material != null)
		{
			// Shader styles (impact_outline.gdshader): 1 = invert, 2 = blackout with diagonal scratches.
			// scratch_seed is rerolled so the scratch pattern changes on each impact.
			material.SetShaderParameter("style", 2);
			material.SetShaderParameter("scratch_seed", Mathf.Randf() * 1000.0f);
		}
		await WaitFrames(ImpactBlackoutFrames);

		rect.Visible = false;
		SetRectAlpha(rect, 1.0f);
		// Restore normal game speed and the camera's original zoom.
		Engine.TimeScale = 1.0f;
		if (material != null)
		{
			material.SetShaderParameter("style", 1);
		}
		if (camera != null)
		{
			camera.Zoom = baseZoom;
		}
		_impactPlaying = false;
	}

	// A Color is a struct, so change its alpha on a copy and assign the copy back.
	private void SetRectAlpha(CanvasItem item, float alpha)
	{
		Color color = item.Modulate;
		color.A = alpha;
		item.Modulate = color;
	}

	public async Task WaitFrames(int frames)
	{
		for (int i = 0; i < Math.Max(1, frames); i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		// Debug: press K to kill both fighters in the same frame.
		if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.K)
		{
			DebugDoubleKo();
		}
	}

	public void DebugDoubleKo()
	{
		Node scene = GetTree().CurrentScene;
		if (scene == null)
		{
			return;
		}
		Player p1 = scene.GetNodeOrNull<Player>("PlayerBody1");
		Player p2 = scene.GetNodeOrNull<Player>("PlayerBody2");
		if (p1 != null)
		{
			p1.Die();
		}
		if (p2 != null)
		{
			p2.Die();
		}
	}

	// Reads and parses res://data/tuning/defaults.json. Returns an empty dictionary and logs an error if the file is missing or the JSON is invalid.
	public Godot.Collections.Dictionary LoadGameData()
	{
		FileAccess file = FileAccess.Open("res://data/tuning/defaults.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError("GameManager.cs: Could not open defaults.json");
			return new Godot.Collections.Dictionary();
		}
		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushError("GameManager.cs: defaults.json is not a valid JSON object");
			return new Godot.Collections.Dictionary();
		}
		return parsed.AsGodotDictionary();
	}

	public void ApplyGameData()
	{
		Godot.Collections.Dictionary matchSection = new Godot.Collections.Dictionary();
		if (GameData.ContainsKey("match") && GameData["match"].VariantType == Variant.Type.Dictionary)
		{
			matchSection = GameData["match"].AsGodotDictionary();
		}
		else
		{
			GD.PushError("GameManager.cs: 'match' section missing from defaults.json, using fallback values");
		}

		int stocks = 3;
		if (matchSection.ContainsKey("stocks_per_round"))
		{
			stocks = (int)matchSection["stocks_per_round"];
		}
		else
		{
			GD.PushError("GameManager.cs: 'stocks_per_round' missing from defaults.json, using fallback 3");
		}

		RoundsToWin = 2;
		if (matchSection.ContainsKey("default_rounds_to_win"))
		{
			RoundsToWin = (int)matchSection["default_rounds_to_win"];
		}
		else
		{
			GD.PushError("GameManager.cs: 'default_rounds_to_win' missing from defaults.json, using fallback 2");
		}
		P1Stocks = stocks;
		P2Stocks = stocks;
	}
}
