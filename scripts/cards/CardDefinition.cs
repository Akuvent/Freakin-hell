using Godot;
using System.Collections.Generic;

public sealed class CardEffect
{
	public string Type = "";
	public string Stat = "";
	public string Op = "";
	public float Value = 0.0f;

	public static CardEffect FromDictionary(Godot.Collections.Dictionary dict)
	{
		CardEffect effect = new CardEffect();
		if (dict.TryGetValue("type", out Variant type))
		{
			effect.Type = type.AsString();
		}
		if (dict.TryGetValue("stat", out Variant stat))
		{
			effect.Stat = stat.AsString();
		}
		if (dict.TryGetValue("op", out Variant op))
		{
			effect.Op = op.AsString();
		}
		if (dict.TryGetValue("value", out Variant value))
		{
			effect.Value = value.AsSingle();
		}
		return effect;
	}
}

public sealed class CardDefinition
{
	public string Id = "";
	public string Name = "";
	public string Rarity = "";
	public string Pool = "";
	public string Trigger = "";
	public List<string> Tags = new List<string>();
	public List<CardEffect> Effects = new List<CardEffect>();

	public static bool TryFromDictionary(Godot.Collections.Dictionary dict, out CardDefinition card)
	{
		card = null;
		if (!dict.TryGetValue("id", out Variant idVariant) || string.IsNullOrEmpty(idVariant.AsString()))
		{
			GD.PushError("CardDefinition: card JSON missing 'id'");
			return false;
		}

		card = new CardDefinition
		{
			Id = idVariant.AsString(),
			Name = ReadString(dict, "name", idVariant.AsString()),
			Rarity = ReadString(dict, "rarity", ""),
			Pool = ReadString(dict, "pool", ""),
			Trigger = ReadString(dict, "trigger", ""),
		};

		if (dict.TryGetValue("tags", out Variant tagsVariant) && tagsVariant.VariantType == Variant.Type.Array)
		{
			foreach (Variant tag in tagsVariant.AsGodotArray())
			{
				card.Tags.Add(tag.AsString());
			}
		}

		if (dict.TryGetValue("effects", out Variant effectsVariant) && effectsVariant.VariantType == Variant.Type.Array)
		{
			foreach (Variant item in effectsVariant.AsGodotArray())
			{
				if (item.VariantType != Variant.Type.Dictionary)
				{
					continue;
				}
				card.Effects.Add(CardEffect.FromDictionary(item.AsGodotDictionary()));
			}
		}

		return true;
	}

	private static string ReadString(Godot.Collections.Dictionary dict, string key, string fallback)
	{
		if (!dict.TryGetValue(key, out Variant value))
		{
			return fallback;
		}
		return value.AsString();
	}
}