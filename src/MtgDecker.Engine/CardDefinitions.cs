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

    static CardDefinitions()
    {
        var cards = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            // === Goblins deck ===
            ["Goblin Lackey"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfDealsCombatDamage, new PutCreatureFromHandEffect("Goblin"))],
            },
            ["Goblin Matron"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SearchLibraryEffect("Goblin", optional: true))],
            },
            ["Goblin Piledriver"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new PiledriverPumpEffect())],
            },
            ["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new RevealAndFilterEffect(4, "Goblin"))],
            },
            ["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
                        GrantedKeyword: Keyword.Haste),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: -1,
                        CostApplies: c => c.Subtypes.Contains("Goblin")),
                ],
            },
            ["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true), new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
            },
            ["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature) { Subtypes = ["Goblin"] },
            ["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin", ManaCost: ManaCost.Parse("{1}{R}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true),
            },
            ["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
                        PowerMod: 1, ToughnessMod: 1),
                ],
            },
            ["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new PyromancerEffect())],
            },
            ["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true), new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
                Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, new UntapSelfEffect())],
            },
            ["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Artifact)),
            },
            ["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin"), new AddManaEffect(ManaColor.Red)),
            },
            ["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
                TargetFilter.EnchantmentOrArtifact(), new NaturalizeEffect()),

            // === Goblins lands ===
            ["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Mountain"] },
            ["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land) { Subtypes = ["Forest"] },
            ["Karplusan Forest"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green), null, null, CardType.Land),
            ["Wooded Foothills"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Mountain", "Forest"]) },
            ["Rishadan Port"] = new(null, null, null, null, CardType.Land)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{1}")), new TapTargetEffect(), c => c.IsLand),
            },
            ["Wasteland"] = new(null, null, null, null, CardType.Land)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true), new DestroyTargetEffect(), c => c.IsLand && !c.IsBasicLand),
            },

            // === Enchantress deck ===
            ["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature | CardType.Enchantment)
            {
                Subtypes = ["Human", "Druid"],
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
            },
            ["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new SwordsToPlowsharesEffect()),
            ["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery),
            ["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
            },
            ["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                Subtypes = ["Aura"],
                AuraTarget = AuraTarget.Land,
            },
            ["Exploration"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ExtraLandDrop,
                        (_, _) => true, ExtraLandDrops: 1),
                ],
            },
            ["Mirri's Guile"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new RearrangeTopEffect(3))],
            },
            ["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment),
                        GrantedKeyword: Keyword.Shroud,
                        ExcludeSelf: true,
                        ControllerOnly: true),
                ],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{1}")), new SearchLibraryByTypeEffect(CardType.Enchantment)),
            },
            ["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: 2,
                        CostApplies: c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment),
                        CostAppliesToOpponent: true),
                ],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true),
                    new DestroyTargetEffect(),
                    c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment)),
            },
            ["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Enchantment) || c.CardTypes.HasFlag(CardType.Artifact)),
            },
            ["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment),
            ["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new SylvanLibraryEffect())],
            },

            // === Enchantress lands ===
            ["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land) { Subtypes = ["Plains"] },
            ["Brushland"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Green, ManaColor.White), null, null, CardType.Land),
            ["Windswept Heath"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Plains", "Forest"]) },
            ["Serra's Sanctum"] = new(null, ManaAbility.Dynamic(ManaColor.White,
                p => p.Battlefield.Cards.Count(c => c.CardTypes.HasFlag(CardType.Enchantment))),
                null, null, CardType.Land) { IsLegendary = true },
        };

        Registry = cards.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGet(string cardName, [NotNullWhen(true)] out CardDefinition? definition)
    {
        return Registry.TryGetValue(cardName, out definition);
    }
}
