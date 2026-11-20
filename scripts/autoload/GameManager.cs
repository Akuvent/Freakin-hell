using Godot;

/// Run-level state: one "game" from menu start until end game (build, match wins in the series).
public partial class GameManager : Node
{
	public Label P1ScoreLabel;
	public Label P2ScoreLabel;
	public int P1Score;
	public int P2Score;

	// Parsed content of res://data/tuning/defaults.json, loaded in _Ready().
	public Godot.Collections.Dictionary GameData = new Godot.Collections.Dictionary();
	// fighter id -> definition object from data/characters/fighters.json
	public Godot.Collections.Dictionary Fighters = new Godot.Collections.Dictionary();

	public override void _Ready()
	{
		GameData = LoadJsonFile("res://data/tuning/defaults.json");
		Fighters = LoadJsonFile("res://data/characters/fighters.json");
	}

	public CharacterStats GetCharacterStats(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			GD.PushError("GameManager: empty character id");
			return new CharacterStats();
		}
		if (TryGetFighterDef(id, out Godot.Collections.Dictionary def))
		{
			return CharacterStats.FromDictionary(id, def);
		}
		GD.PushError($"GameManager: character '{id}' missing from fighters.json");
		return CharacterStats.FromDictionary("default", new Godot.Collections.Dictionary());
	}

	public void BindScoreLabels(Label p1Score, Label p2Score)
	{
		P1ScoreLabel = p1Score;
		P2ScoreLabel = p2Score;
		RefreshScoreLabels();
	}

	public void RefreshScoreLabels()
	{
		if (P1ScoreLabel != null)
		{
			P1ScoreLabel.Text = $"{P1Score}";
		}
		if (P2ScoreLabel != null)
		{
			P2ScoreLabel.Text = $"{P2Score}";
		}
	}

	// Match wins in the current run. Build state will reset here too when that exists.
	public void RecordMatchWin(int playerNumber)
	{
		if (playerNumber == 1)
		{
			P1Score++;
		}
		else if (playerNumber == 2)
		{
			P2Score++;
		}
		else
		{
			return;
		}
		RefreshScoreLabels();
	}

	// Full run reset (end game). Does not reload scenes; call MatchManager when the arena should reset.
	public void ResetRun()
	{
		P1Score = 0;
		P2Score = 0;
		RefreshScoreLabels();
	}

	private bool TryGetFighterDef(string id, out Godot.Collections.Dictionary def)
	{
		def = new Godot.Collections.Dictionary();
		if (Fighters.TryGetValue(id, out Variant defVariant) && defVariant.VariantType == Variant.Type.Dictionary)
		{
			def = defVariant.AsGodotDictionary();
			return true;
		}
		if (id != "default" && Fighters.TryGetValue("default", out Variant fallback) && fallback.VariantType == Variant.Type.Dictionary)
		{
			def = fallback.AsGodotDictionary();
			return true;
		}
		return false;
	}

	public Godot.Collections.Dictionary LoadJsonFile(string path)
	{
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"GameManager.cs: Could not open {path}");
			return new Godot.Collections.Dictionary();
		}
		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushError($"GameManager.cs: {path} is not a valid JSON object");
			return new Godot.Collections.Dictionary();
		}
		return parsed.AsGodotDictionary();
	}
}
