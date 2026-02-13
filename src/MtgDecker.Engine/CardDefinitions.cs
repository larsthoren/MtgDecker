using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine;

public static class CardDefinitions
{
    private static readonly FrozenDictionary<string, CardDefinition> Registry;
    private static readonly ConcurrentDictionary<string, CardDefinition> RuntimeOverrides = new(StringComparer.OrdinalIgnoreCase);

    static CardDefinitions()
    {
        var cards = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            // === Goblins deck ===
            ["Goblin Lackey"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Goblin Matron"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SearchLibraryEffect("Goblin", optional: true))],
            },
            ["Goblin Piledriver"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new RevealAndFilterEffect(4, "Goblin"))],
            },
            ["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))],
            },
            ["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
                TargetFilter.EnchantmentOrArtifact(), new NaturalizeEffect()),

            // === Goblins lands ===
            ["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land),
            ["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land),
            ["Karplusan Forest"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green), null, null, CardType.Land),
            ["Wooded Foothills"] = new(null, null, null, null, CardType.Land),
            ["Rishadan Port"] = new(null, null, null, null, CardType.Land),
            ["Wasteland"] = new(null, null, null, null, CardType.Land),

            // === Enchantress deck ===
            ["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature | CardType.Enchantment) { Subtypes = ["Human", "Druid"] },
            ["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new SwordsToPlowsharesEffect()),
            ["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery),
            ["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment),
            ["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment) { Subtypes = ["Aura"] },
            ["Exploration"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
            ["Mirri's Guile"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
            ["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment),
            ["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment),
            ["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment),
            ["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment),

            // === Enchantress lands ===
            ["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land),
            ["Brushland"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Green, ManaColor.White), null, null, CardType.Land),
            ["Windswept Heath"] = new(null, null, null, null, CardType.Land),
            ["Serra's Sanctum"] = new(null, null, null, null, CardType.Land),

            // === Burn deck ===
            ["Lightning Bolt"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Chain Lightning"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Lava Spike"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new DamageEffect(3, canTargetCreature: false)),
            ["Rift Bolt"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Fireblast"] = new(ManaCost.Parse("{4}{R}{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(4)),
            ["Goblin Guide"] = new(ManaCost.Parse("{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Monastery Swiftspear"] = new(ManaCost.Parse("{R}"), null, 1, 2, CardType.Creature) { Subtypes = ["Human", "Monk"] },
            ["Eidolon of the Great Revel"] = new(ManaCost.Parse("{R}{R}"), null, 2, 2,
                CardType.Creature | CardType.Enchantment) { Subtypes = ["Spirit"] },
            ["Searing Blood"] = new(ManaCost.Parse("{R}{R}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new DamageEffect(2, canTargetPlayer: false)),
            ["Flame Rift"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DamageAllPlayersEffect(4)),

            // === UR Delver deck ===
            ["Brainstorm"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                Effect: new BrainstormEffect()),
            ["Ponder"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                Effect: new PonderEffect()),
            ["Preordain"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                Effect: new PreordainEffect()),
            ["Counterspell"] = new(ManaCost.Parse("{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect()),
            ["Daze"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect()),
            ["Force of Will"] = new(ManaCost.Parse("{3}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect()),
            ["Delver of Secrets"] = new(ManaCost.Parse("{U}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Wizard"] },
            ["Murktide Regent"] = new(ManaCost.Parse("{5}{U}{U}"), null, 3, 3, CardType.Creature) { Subtypes = ["Dragon"] },
            ["Dragon's Rage Channeler"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Shaman"] },

            // === UR Delver lands ===
            ["Island"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land),
            ["Volcanic Island"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Red), null, null, CardType.Land),
            ["Scalding Tarn"] = new(null, null, null, null, CardType.Land),
            ["Mystic Sanctuary"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land),
        };

        Registry = cards.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGet(string cardName, [NotNullWhen(true)] out CardDefinition? definition)
    {
        if (RuntimeOverrides.TryGetValue(cardName, out definition))
            return true;
        return Registry.TryGetValue(cardName, out definition);
    }

    /// <summary>
    /// Register a card definition at runtime (for new card sets, tests, etc.).
    /// Runtime registrations take priority over the built-in registry.
    /// </summary>
    public static void Register(CardDefinition definition)
    {
        RuntimeOverrides[definition.Name] = definition;
    }

    /// <summary>
    /// Remove a runtime-registered card definition.
    /// </summary>
    public static bool Unregister(string cardName)
    {
        return RuntimeOverrides.TryRemove(cardName, out _);
    }
}
