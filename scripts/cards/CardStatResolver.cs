using System.Collections.Generic;
using Godot;

/// Applies modify_stat effects for one trigger (adds first, then multipliers).
public static class CardStatResolver
{
	public static float Resolve(IReadOnlyList<CardDefinition> cards, string trigger, string stat, float baseValue)
	{
		if (cards == null || cards.Count == 0)
		{
			return baseValue;
		}
		float sumAdd = 0.0f;
		float productMult = 1.0f;
		foreach (CardDefinition card in cards)
		{
			if (card.Trigger != trigger)
			{
				continue;
			}
			foreach (CardEffect effect in card.Effects)
			{
				if (effect.Type != "modify_stat" || effect.Stat != stat)
				{
					continue;
				}
				if (effect.Op == "add")
				{
					sumAdd += effect.Value;
				}
				else if (effect.Op == "multiply")
				{
					productMult *= effect.Value;
				}
				else
				{
					GD.PushError($"CardStatResolver: unknown op '{effect.Op}' on card '{card.Id}'");
				}
			}
		}
		return (baseValue + sumAdd) * productMult;
	}
}