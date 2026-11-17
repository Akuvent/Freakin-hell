using Godot;
using System;
using System.Threading.Tasks;

/// Match and round flow: stocks, round clock, arena reload, combat HUD and VFX.
public partial class MatchManager : Node
{
	public const int ImpactInvertFrames = 5;
	public const int ImpactBlackoutFrames = 5;
	public const float ImpactTimeScale = 0.05f;

	public Label P1StocksLabel;
	public Label P2StocksLabel;
	public Label P1HpLabel;
	public Label P2HpLabel;
	private Label _winLabel;
	public int P1Stocks;
	public int P2Stocks;

	private bool _impactPlaying;
	private bool _roundResolving;
	public int RoundsToWin;
	public int P1RoundsWon;
	public int P2RoundsWon;
	public float RoundClock;
	private bool _roundClockRunning;

	private GameManager _gameManager;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_gameManager = GetNode<GameManager>("/root/GameManager");
		CallDeferred(MethodName.BindArenaUI);
	}

	public void BindArenaUI()
	{
		Node scene = GetTree().CurrentScene;
		if (scene == null)
		{
			return;
		}
		P1HpLabel = scene.GetNodeOrNull("UI/P1_Hp") as Label;
		P2HpLabel = scene.GetNodeOrNull("UI/P2_Hp") as Label;
		P1StocksLabel = scene.GetNodeOrNull("UI/P1_Stocks") as Label;
		P2StocksLabel = scene.GetNodeOrNull("UI/P2_Stocks") as Label;
		Label p1Score = scene.GetNodeOrNull("UI/P1_Score") as Label;
		Label p2Score = scene.GetNodeOrNull("UI/P2_Score") as Label;
		_winLabel = scene.GetNodeOrNull("UI/WinLabel") as Label;
		_gameManager.BindScoreLabels(p1Score, p2Score);

		ApplyMatchTuningFromGameData();
		RefreshStockLabels();
		StartRoundClock();

		Player p1 = scene.GetNodeOrNull<Player>("PlayerBody1");
		Player p2 = scene.GetNodeOrNull<Player>("PlayerBody2");
		Callable onP1Died = new Callable(this, MethodName.OnP1Died);
		Callable onP2Died = new Callable(this, MethodName.OnP2Died);
		Callable onP1HpChanged = new Callable(this, MethodName.OnP1HpChanged);
		Callable onP2HpChanged = new Callable(this, MethodName.OnP2HpChanged);
		if (p1 != null && !p1.IsConnected(Player.SignalName.Died, onP1Died))
		{
			p1.Connect(Player.SignalName.Died, onP1Died);
		}
		if (p2 != null && !p2.IsConnected(Player.SignalName.Died, onP2Died))
		{
			p2.Connect(Player.SignalName.Died, onP2Died);
		}
		if (p1 != null && !p1.IsConnected(Player.SignalName.HpChanged, onP1HpChanged))
		{
			p1.Connect(Player.SignalName.HpChanged, onP1HpChanged);
		}
		if (p2 != null && !p2.IsConnected(Player.SignalName.HpChanged, onP2HpChanged))
		{
			p2.Connect(Player.SignalName.HpChanged, onP2HpChanged);
		}
		if (p1 != null)
		{
			SetHpLabel(P1HpLabel, p1.CurrentHp);
		}
		if (p2 != null)
		{
			SetHpLabel(P2HpLabel, p2.CurrentHp);
		}
	}

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

	// Zeros match-round wins and reloads the arena. Keeps run score on GameManager.
	public async Task StartNewMatch()
	{
		P1RoundsWon = 0;
		P2RoundsWon = 0;
		Engine.TimeScale = 1.0f;
		_impactPlaying = false;
		GetTree().Paused = false;
		await ReloadArena();
	}

	public void ResetMatchStateForNewRun()
	{
		P1RoundsWon = 0;
		P2RoundsWon = 0;
		P1Stocks = 0;
		P2Stocks = 0;
		RoundClock = 0.0f;
		_roundClockRunning = false;
		_impactPlaying = false;
		_roundResolving = false;
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

	public void OnP1HpChanged(float hp)
	{
		SetHpLabel(P1HpLabel, hp);
	}

	public void OnP2HpChanged(float hp)
	{
		SetHpLabel(P2HpLabel, hp);
	}

	public void SetHpLabel(Label label, float hp)
	{
		if (label == null)
		{
			return;
		}
		float damageFraction = Mathf.Clamp((Player.MaxHp - hp) / Player.MaxHp, 0.0f, 1.0f);
		Color color;
		if (damageFraction < 0.5f)
		{
			color = Colors.White.Lerp(Colors.Yellow, damageFraction * 2.0f);
		}
		else
		{
			color = Colors.Yellow.Lerp(Colors.Red, (damageFraction - 0.5f) * 2.0f);
		}
		label.Text = $"{Mathf.RoundToInt(hp)} HP";
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

	public async void ResolveRoundSoon()
	{
		if (_roundResolving)
		{
			return;
		}
		_roundResolving = true;
		_roundClockRunning = false;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		bool matchOver = false;
		int matchWinner = 0;
		if (P1Stocks <= 0 && P2Stocks <= 0)
		{
			GD.Print("Draw: no round win");
		}
		else if (P1Stocks <= 0)
		{
			P2RoundsWon++;
			if (P2RoundsWon == RoundsToWin)
			{
				matchWinner = 2;
				GD.Print("Match over: P2 wins");
				matchOver = true;
			}
		}
		else if (P2Stocks <= 0)
		{
			P1RoundsWon++;
			if (P1RoundsWon == RoundsToWin)
			{
				matchWinner = 1;
				GD.Print("Match over: P1 wins");
				matchOver = true;
			}
		}

		try
		{
			if (matchOver)
			{
				if (matchWinner != 0)
				{
					_gameManager.RecordMatchWin(matchWinner);
					await WinAnimation(matchWinner);
				}
				await StartNewMatch();
			}
			else
			{
				await ReloadArena();
			}
		}
		finally
		{
			_roundResolving = false;
		}
	}

	public async Task ReloadArena()
	{
		SceneTree tree = GetTree();
		string path = tree.CurrentScene.SceneFilePath;
		if (string.IsNullOrEmpty(path))
		{
			GD.PushError("MatchManager: no scene path to reload");
			return;
		}
		tree.ChangeSceneToFile(path);

		for (int i = 0; i < 10; i++)
		{
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			if (tree.CurrentScene != null && tree.CurrentScene.GetNodeOrNull("UI/P1_Hp") != null && tree.CurrentScene.GetNodeOrNull("UI/P2_Hp") != null)
			{
				BindArenaUI();
				return;
			}
		}
		GD.PushError("MatchManager: failed to reload arena, no UI found");
	}

	public void PlayLightHitFx(string kind)
	{
		Node scene = GetTree().CurrentScene;
		CameraShake camera = scene?.GetNodeOrNull<CameraShake>("Camera2D");
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
			camera.Zoom = baseZoom * 2.06f;
			camera.AddTrauma(0.8f, "heavy");
		}
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
			material.SetShaderParameter("style", 2);
			material.SetShaderParameter("scratch_seed", GD.Randf() * 1000.0f);
		}
		await WaitFrames(ImpactBlackoutFrames);

		rect.Visible = false;
		SetRectAlpha(rect, 1.0f);
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

	public void ApplyMatchTuningFromGameData()
	{
		Godot.Collections.Dictionary gameData = _gameManager.GameData;
		Godot.Collections.Dictionary matchSection = new Godot.Collections.Dictionary();
		if (gameData.ContainsKey("match") && gameData["match"].VariantType == Variant.Type.Dictionary)
		{
			matchSection = gameData["match"].AsGodotDictionary();
		}
		else
		{
			GD.PushError("MatchManager: 'match' section missing from defaults.json, using fallback values");
		}

		int stocks = 3;
		if (matchSection.ContainsKey("stocks_per_round"))
		{
			stocks = (int)matchSection["stocks_per_round"];
		}
		else
		{
			GD.PushError("MatchManager: 'stocks_per_round' missing from defaults.json, using fallback 3");
		}

		RoundsToWin = 2;
		if (matchSection.ContainsKey("default_rounds_to_win"))
		{
			RoundsToWin = (int)matchSection["default_rounds_to_win"];
		}
		else
		{
			GD.PushError("MatchManager: 'default_rounds_to_win' missing from defaults.json, using fallback 2");
		}
		P1Stocks = stocks;
		P2Stocks = stocks;
	}

	private async Task WinAnimation(int winner)
	{
		if (_winLabel == null)
		{
			GD.PushError("MatchManager: UI/WinLabel not found");
			return;
		}

		SceneTree tree = GetTree();
		tree.Paused = true;

		float restTop = _winLabel.OffsetTop;
		const float slideOffset = 48.0f;

		_winLabel.Text = $"PLAYER {winner} WINS!";
		_winLabel.Modulate = new Color(1f, 1f, 1f, 0f);
		_winLabel.OffsetTop = restTop - slideOffset;
		_winLabel.Visible = true;

		Tween tween = CreateTween();
		tween.TweenProperty(_winLabel, "modulate:a", 1.0f, 0.5f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(_winLabel, "offset_top", restTop, 0.5f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		await ToSignal(tween, Tween.SignalName.Finished);

		await ToSignal(tree.CreateTimer(1.5, true), SceneTreeTimer.SignalName.Timeout);

		Tween tweenOut = CreateTween();
		tweenOut.TweenProperty(_winLabel, "modulate:a", 0.0f, 0.5f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		tweenOut.Parallel().TweenProperty(_winLabel, "offset_top", restTop - slideOffset, 0.5f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		await ToSignal(tweenOut, Tween.SignalName.Finished);

		_winLabel.Visible = false;
		_winLabel.OffsetTop = restTop;
		_winLabel.Modulate = Colors.White;
	}
}
