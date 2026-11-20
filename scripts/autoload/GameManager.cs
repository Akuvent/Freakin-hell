using Godot;
using System.Collections.Generic;

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
	public Dictionary<string, CardDefinition> CardsById = new Dictionary<string, CardDefinition>();
	private readonly List<string> _p1EquippedCardIds = new List<string>();
	private readonly List<string> _p2EquippedCardIds = new List<string>();

	public override void _Ready()
	{
		GameData = LoadJsonFile("res://data/tuning/defaults.json");
		Fighters = LoadJsonFile("res://data/characters/fighters.json");
		CardsById = CardLoader.LoadAll();
		ApplySandboxBuildFromGameData();
	}

	public IReadOnlyList<CardDefinition> GetEquippedCards(int playerSlot)
	{
		List<CardDefinition> equipped = new List<CardDefinition>();
		List<string> ids = playerSlot == 1 ? _p1EquippedCardIds : _p2EquippedCardIds;
		foreach (string id in ids)
		{
			if (TryGetCard(id, out CardDefinition card))
			{
				equipped.Add(card);
			}
			else
			{
				GD.PushError($"GameManager: equipped card '{id}' for player {playerSlot} not loaded");
			}
		}
		return equipped;
	}

	public void EquipCard(int playerSlot, string cardId)
	{
		if (!TryGetCard(cardId, out _))
		{
			GD.PushError($"GameManager: cannot equip unknown card '{cardId}'");
			return;
		}
		List<string> ids = playerSlot == 1 ? _p1EquippedCardIds : _p2EquippedCardIds;
		if (!ids.Contains(cardId))
		{
			ids.Add(cardId);
		}
	}

	public void ClearBuild(int playerSlot)
	{
		if (playerSlot == 1)
		{
			_p1EquippedCardIds.Clear();
		}
		else
		{
			_p2EquippedCardIds.Clear();
		}
	}

	private void ApplySandboxBuildFromGameData()
	{
		_p1EquippedCardIds.Clear();
		_p2EquippedCardIds.Clear();
		if (!GameData.TryGetValue("build", out Variant buildVariant) || buildVariant.VariantType != Variant.Type.Dictionary)
		{
			return;
		}
		Godot.Collections.Dictionary build = buildVariant.AsGodotDictionary();
		AppendCardIdsFromJson(build, "sandbox_p1_cards", _p1EquippedCardIds);
		AppendCardIdsFromJson(build, "sandbox_p2_cards", _p2EquippedCardIds);
	}

	private void AppendCardIdsFromJson(Godot.Collections.Dictionary build, string key, List<string> target)
	{
		if (!build.TryGetValue(key, out Variant listVariant) || listVariant.VariantType != Variant.Type.Array)
		{
			return;
		}
		foreach (Variant item in listVariant.AsGodotArray())
		{
			string id = item.AsString();
			if (!string.IsNullOrEmpty(id))
			{
				target.Add(id);
			}
		}
	}

	public bool TryGetCard(string id, out CardDefinition card)
	{
		if (string.IsNullOrEmpty(id))
		{
			card = null;
			return false;
		}
		return CardsById.TryGetValue(id, out card);
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
		ApplySandboxBuildFromGameData();
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
