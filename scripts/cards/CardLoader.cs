using Godot;
using System.Collections.Generic;

public static class CardLoader
{
	public const string CardsDirectory = "res://data/cards";

	/// Loads every *.json file in data/cards. Skips invalid files with errors; never throws.
	public static Dictionary<string, CardDefinition> LoadAll()
	{
		Dictionary<string, CardDefinition> cardsById = new Dictionary<string, CardDefinition>();
		using DirAccess dir = DirAccess.Open(CardsDirectory);
		if (dir == null)
		{
			GD.PushError($"CardLoader: could not open {CardsDirectory}");
			return cardsById;
		}

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (fileName != "")
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
			{
				string path = $"{CardsDirectory}/{fileName}";
				TryLoadOne(path, cardsById);
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();
		return cardsById;
	}

	private static void TryLoadOne(string path, Dictionary<string, CardDefinition> cardsById)
	{
		Godot.Collections.Dictionary raw = LoadJsonAt(path);
		if (raw.Count == 0)
		{
			return;
		}
		if (!CardDefinition.TryFromDictionary(raw, out CardDefinition card))
		{
			GD.PushError($"CardLoader: skip invalid card at {path}");
			return;
		}
		if (cardsById.ContainsKey(card.Id))
		{
			GD.PushError($"CardLoader: duplicate card id '{card.Id}' at {path}");
			return;
		}
		cardsById[card.Id] = card;
	}

	private static Godot.Collections.Dictionary LoadJsonAt(string path)
	{
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"CardLoader: could not open {path}");
			return new Godot.Collections.Dictionary();
		}
		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushError($"CardLoader: {path} is not a JSON object");
			return new Godot.Collections.Dictionary();
		}
		return parsed.AsGodotDictionary();
	}
}