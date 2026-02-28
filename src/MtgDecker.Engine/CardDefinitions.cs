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
                Subtypes = ["Goblin", "Warrior"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Goblin Piledriver",
                        GrantedKeyword: Keyword.ProtectionFromBlue,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new PiledriverPumpEffect())],
            },
            ["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new RevealAndFilterEffect(4, "Goblin"))],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Goblin Ringleader",
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin", "Warrior"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: -1,
                        CostApplies: c => c.Subtypes.Contains("Goblin")),
                ],
            },
            ["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSelf: true), new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true)],
            },
            ["Gempalm Incinerator"] = new(ManaCost.Parse("{2}{R}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                CyclingCost = ManaCost.Parse("{1}{R}"),
                CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new GempalmIncineratorEffect())],
            },
            ["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3, tokenColors: [ManaColor.Red]))],
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin", ManaCost: ManaCost.Parse("{1}{R}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true)],
            },
            ["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
                        PowerMod: 1, ToughnessMod: 1,
                        ExcludeSelf: true,
                        Layer: EffectLayer.Layer7c_ModifyPT),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
                        GrantedKeyword: Keyword.Mountainwalk,
                        ExcludeSelf: true,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin", "Wizard"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new PyromancerEffect())],
            },
            ["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Goblin Sharpshooter",
                        GrantedKeyword: Keyword.DoesNotUntap,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true), new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true)],
                Triggers = [new Trigger(GameEvent.Dies, TriggerCondition.AnyCreatureDies, new UntapSelfEffect())],
            },
            ["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{R}")), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Artifact))],
            },
            ["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSubtype: "Goblin"), new AddManaEffect(ManaColor.Red))],
            },
            ["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
                TargetFilter.EnchantmentOrArtifact(), new DestroyTargetSpellEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },

            // === Goblins lands ===
            ["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Mountain"] },
            ["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land) { Subtypes = ["Forest"] },
            ["Karplusan Forest"] = new(null, ManaAbility.PainChoice(
                [ManaColor.Colorless, ManaColor.Red, ManaColor.Green],
                [ManaColor.Red, ManaColor.Green]), null, null, CardType.Land),
            ["Wooded Foothills"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Mountain", "Forest"]) },
            ["Rishadan Port"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{1}")), new TapTargetEffect(), c => c.IsLand)],
            },
            ["Wasteland"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true), new DestroyTargetEffect(), c => c.IsLand && !c.IsBasicLand)],
            },

            // === Enchantress deck ===
            ["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Druid"],
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Argothian Enchantress",
                        GrantedKeyword: Keyword.Shroud,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new SwordsToPlowsharesEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery,
                null, new ReplenishEffect()),
            ["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsEnchantment, new DrawCardEffect())],
            },
            ["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                Subtypes = ["Aura"],
                AuraTarget = AuraTarget.Land,
                Triggers = [new Trigger(GameEvent.TapForMana, TriggerCondition.AttachedPermanentTapped, new AddBonusManaEffect(ManaColor.Green))],
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
            ["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.BecomeCreature,
                        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment)
                            && !card.Subtypes.Contains("Aura"),
                        SetPowerToughnessToCMC: true,
                        Layer: EffectLayer.Layer4_TypeChanging),
                ],
            },
            ["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
            {
                EntersWithCounters = new() { [CounterType.Fade] = 5 },
                Triggers =
                [
                    new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new FadingUpkeepEffect()),
                    new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.SelfLeavesBattlefield, new ReturnExiledCardsEffect()),
                ],
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(RemoveCounterType: CounterType.Fade),
                    new ExileCreatureEffect(),
                    c => c.IsCreature)],
            },
            ["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.CardTypes.HasFlag(CardType.Enchantment),
                        GrantedKeyword: Keyword.Shroud,
                        ExcludeSelf: true,
                        ControllerOnly: true,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{1}")), new SearchLibraryEffect(c => c.CardTypes.HasFlag(CardType.Enchantment), "Enchantment", SearchDestination.TopOfLibrary))],
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
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSelf: true),
                    new DestroyTargetEffect(),
                    c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment))],
            },
            ["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSelf: true), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Enchantment) || c.CardTypes.HasFlag(CardType.Artifact))],
            },
            ["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new UpkeepCostEffect())],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.SkipDraw, (_, _) => true),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantPlayerShroud, (_, _) => true),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventDamageToPlayer, (_, _) => true),
                ],
            },
            ["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new SylvanLibraryEffect())],
            },

            // === Enchantress lands ===
            ["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land) { Subtypes = ["Plains"] },
            ["Brushland"] = new(null, ManaAbility.PainChoice(
                [ManaColor.Colorless, ManaColor.Green, ManaColor.White],
                [ManaColor.Green, ManaColor.White]), null, null, CardType.Land),
            ["Windswept Heath"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Plains", "Forest"]) },
            ["Serra's Sanctum"] = new(null, ManaAbility.Dynamic(ManaColor.White,
                p => p.Battlefield.Cards.Count(c => c.CardTypes.HasFlag(CardType.Enchantment))),
                null, null, CardType.Land) { IsLegendary = true },

            // === Burn deck ===
            ["Lightning Bolt"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3))
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Chain Lightning"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Lava Spike"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new DamageEffect(3, canTargetCreature: false)),
            ["Rift Bolt"] = new(ManaCost.Parse("{2}{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Fireblast"] = new(ManaCost.Parse("{4}{R}{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(4))
            {
                AlternateCost = new AlternateCost(SacrificeLandSubtype: "Mountain", SacrificeLandCount: 2),
            },
            ["Goblin Guide"] = new(ManaCost.Parse("{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin", "Scout"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Goblin Guide",
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks,
                    new GoblinGuideRevealEffect())],
            },
            ["Monastery Swiftspear"] = new(ManaCost.Parse("{R}"), null, 1, 2, CardType.Creature)
            {
                Subtypes = ["Human", "Monk"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Monastery Swiftspear",
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.ControllerCastsNoncreature, new ProwessEffect())],
            },
            ["Eidolon of the Great Revel"] = new(ManaCost.Parse("{R}{R}"), null, 2, 2,
                CardType.Creature | CardType.Enchantment)
            {
                Subtypes = ["Spirit"],
                Triggers = [new Trigger(GameEvent.SpellCast,
                    TriggerCondition.AnySpellCastCmc3OrLess, new DealDamageEffect(2))],
            },
            ["Searing Blood"] = new(ManaCost.Parse("{R}{R}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new SearingBloodEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Flame Rift"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DamageAllPlayersEffect(4)),

            // === UR Delver deck ===
            ["Brainstorm"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                Effect: new BrainstormEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },
            ["Ponder"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                Effect: new PonderEffect()),
            ["Preordain"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                Effect: new PreordainEffect()),
            ["Counterspell"] = new(ManaCost.Parse("{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect())
            {
                SpellRole = SpellRole.Counterspell,
            },
            ["Daze"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new ConditionalCounterEffect(1))
            {
                AlternateCost = new AlternateCost(ReturnLandSubtype: "Island"),
                SpellRole = SpellRole.Counterspell,
            },
            ["Force of Will"] = new(ManaCost.Parse("{3}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect())
            {
                AlternateCost = new AlternateCost(LifeCost: 1, ExileCardColor: ManaColor.Blue),
                SpellRole = SpellRole.Counterspell,
            },
            ["Delver of Secrets"] = new(ManaCost.Parse("{U}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Wizard"] },
            ["Murktide Regent"] = new(ManaCost.Parse("{5}{U}{U}"), null, 3, 3, CardType.Creature)
            {
                Subtypes = ["Dragon"],
                HasDelve = true,
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Murktide Regent",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Dragon's Rage Channeler"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Shaman"] },

            // === UR Delver lands ===
            ["Island"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land) { Subtypes = ["Island"] },
            ["Volcanic Island"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Island", "Mountain"] },
            ["Scalding Tarn"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Island", "Mountain"]) },
            ["Mystic Sanctuary"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land)
            {
                Subtypes = ["Island"],
                ConditionalEntersTapped = p => p.Battlefield.Cards.Count(c =>
                    c.Subtypes.Contains("Island", StringComparer.OrdinalIgnoreCase)) < 3,
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new ReturnSpellToTopEffect())],
            },

            // === Shared Premodern cards ===

            // Basic land
            ["Swamp"] = new(null, ManaAbility.Fixed(ManaColor.Black), null, null, CardType.Land) { Subtypes = ["Swamp"] },

            // Dual/Pain lands
            ["Caves of Koilos"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.White, ManaColor.Black], [ManaColor.White, ManaColor.Black]), null, null, CardType.Land),
            ["Llanowar Wastes"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.Black, ManaColor.Green], [ManaColor.Black, ManaColor.Green]), null, null, CardType.Land),
            ["Battlefield Forge"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.Red, ManaColor.White], [ManaColor.Red, ManaColor.White]), null, null, CardType.Land),
            ["Tainted Field"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.White, ManaColor.Black), null, null, CardType.Land),
            ["Coastal Tower"] = new(null, ManaAbility.Choice(ManaColor.White, ManaColor.Blue), null, null, CardType.Land) { EntersTapped = true },
            ["Skycloud Expanse"] = new(null, ManaAbility.Filter(ManaCost.Parse("{1}"), ManaColor.White, ManaColor.Blue), null, null, CardType.Land),
            ["Adarkar Wastes"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.White, ManaColor.Blue], [ManaColor.White, ManaColor.Blue]), null, null, CardType.Land),
            ["Gemstone Mine"] = new(null, ManaAbility.DepletionChoice(CounterType.Mining, ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green), null, null, CardType.Land)
            {
                EntersWithCounters = new() { [CounterType.Mining] = 3 },
            },
            ["City of Brass"] = new(null, ManaAbility.PainChoice(
                [ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green],
                [ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green]), null, null, CardType.Land),
            ["Darigaaz's Caldera"] = new(null, ManaAbility.Choice(ManaColor.Black, ManaColor.Red, ManaColor.Green), null, null, CardType.Land)
            {
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new BounceLandETBEffect())],
            },
            ["Treva's Ruins"] = new(null, ManaAbility.Choice(ManaColor.White, ManaColor.Blue, ManaColor.Green), null, null, CardType.Land)
            {
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new BounceLandETBEffect())],
            },

            // Fetch lands
            ["Flooded Strand"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Plains", "Island"]) },
            ["Bloodstained Mire"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Swamp", "Mountain"]) },

            // Mana
            ["Dark Ritual"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                Effect: new AddManaSpellEffect(ManaColor.Black, 3))
            {
                SpellRole = SpellRole.Ramp,
            },
            ["Mox Diamond"] = new(ManaCost.Parse("{0}"), ManaAbility.Choice(ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green), null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new MoxDiamondETBEffect())],
            },

            // Common removal
            ["Disenchant"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Instant,
                TargetFilter.EnchantmentOrArtifact(), new DestroyTargetSpellEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Vindicate"] = new(ManaCost.Parse("{1}{W}{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.AnyPermanent(), new DestroyTargetSpellEffect()),
            ["Smother"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureWithCMCAtMost(3), new DestroyTargetSpellEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Snuff Out"] = new(ManaCost.Parse("{3}{B}"), null, null, null, CardType.Instant,
                TargetFilter.NonBlackCreature(), new DestroyTargetSpellEffect())
            {
                AlternateCost = new AlternateCost(LifeCost: 4, RequiresControlSubtype: "Swamp"),
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Diabolic Edict"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new EdictEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Wrath of God"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Sorcery,
                Effect: new DestroyAllCreaturesEffect()),
            ["Armageddon"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery,
                Effect: new DestroyAllLandsEffect()),

            // Common discard
            ["Duress"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new DuressEffect()),
            ["Cabal Therapy"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new CabalTherapyEffect())
            {
                FlashbackCost = new FlashbackCost(SacrificeCreature: true),
            },
            ["Gerrard's Verdict"] = new(ManaCost.Parse("{W}{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new GerrardVerdictEffect()),

            // Common counterspells
            ["Mana Leak"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new ConditionalCounterEffect(3))
            {
                SpellRole = SpellRole.Counterspell,
            },
            ["Absorb"] = new(ManaCost.Parse("{W}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterAndGainLifeEffect(3))
            {
                SpellRole = SpellRole.Counterspell,
            },

            // Common utility
            ["Fact or Fiction"] = new(ManaCost.Parse("{3}{U}"), null, null, null, CardType.Instant,
                Effect: new FactOrFictionEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },
            ["Impulse"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                Effect: new ImpulseEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },
            ["Deep Analysis"] = new(ManaCost.Parse("{3}{U}"), null, null, null, CardType.Sorcery,
                Effect: new DrawCardsEffect(2))
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{1}{U}"), LifeCost: 3),
            },

            // === Sligh (RDW) deck ===
            ["Ball Lightning"] = new(ManaCost.Parse("{R}{R}{R}"), null, 6, 1, CardType.Creature)
            {
                Subtypes = ["Elemental"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Ball Lightning",
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Ball Lightning",
                        GrantedKeyword: Keyword.Trample,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new RegisterEndOfTurnSacrificeEffect())],
            },
            ["Grim Lavamancer"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Wizard"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{R}"), ExileFromGraveyardCount: 2), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true)],
            },
            ["Jackal Pup"] = new(ManaCost.Parse("{R}"), null, 2, 1, CardType.Creature) { Subtypes = ["Jackal"] },
            ["Incinerate"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3))
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Shock"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(2))
            {
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Sulfuric Vortex"] = new(ManaCost.Parse("{1}{R}{R}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new DamageAllPlayersTriggerEffect(2))],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventLifeGain,
                        (_, _) => true),
                ],
            },
            ["Cursed Scroll"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{3}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true)],
            },
            ["Barbarian Ring"] = new(null, ManaAbility.Fixed(ManaColor.Red, selfDamage: 1), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{R}")),
                    new DealDamageEffect(2),
                    TargetFilter: c => c.IsCreature,
                    CanTargetPlayer: true,
                    Condition: p => p.Graveyard.Count >= 7)],
            },

            // === Mono Black Control deck ===
            ["Bane of the Living"] = new(ManaCost.Parse("{2}{B}{B}"), null, 4, 3, CardType.Creature) { Subtypes = ["Insect"] },
            ["Plague Spitter"] = new(ManaCost.Parse("{2}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Phyrexian", "Horror"],
                Triggers =
                [
                    new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep,
                        new DamageAllCreaturesTriggerEffect(1, includePlayers: true)),
                    new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.SelfLeavesBattlefield,
                        new DamageAllCreaturesTriggerEffect(1, includePlayers: true)),
                ],
            },
            ["Withered Wretch"] = new(ManaCost.Parse("{B}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Zombie", "Cleric"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")),
                    new ExileFromAnyGraveyardEffect())],
            },
            ["Funeral Charm"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new DiscardEffect(1))
            {
                SpellRole = SpellRole.InstantUtility,
            },
            ["Bottomless Pit"] = new(ManaCost.Parse("{1}{B}{B}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.AnyUpkeep, new ActivePlayerDiscardsRandomEffect(1))],
            },
            ["The Rack"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new RackDamageEffect())],
            },
            ["Powder Keg"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new PowderKegUpkeepEffect())],
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true),
                    new PowderKegDestroyEffect())],
            },
            ["Cabal Pit"] = new(null, ManaAbility.Fixed(ManaColor.Black, selfDamage: 1), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{B}")),
                    new WeakenTargetEffect(-2, -2),
                    TargetFilter: c => c.IsCreature,
                    Condition: p => p.Graveyard.Count >= 7)],
            },
            ["Dust Bowl"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeCardType: CardType.Land, ManaCost: ManaCost.Parse("{3}")),
                    new DestroyTargetEffect(),
                    TargetFilter: c => c.CardTypes.HasFlag(CardType.Land)
                        && c.Name != "Plains" && c.Name != "Island" && c.Name != "Swamp"
                        && c.Name != "Mountain" && c.Name != "Forest")],
            },

            // === Mono Black Aggro deck ===
            ["Hypnotic Specter"] = new(ManaCost.Parse("{1}{B}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Specter"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Hypnotic Specter",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.CombatDamageDealt, TriggerCondition.SelfDealsCombatDamage, new OpponentDiscardsEffect(1))],
            },
            ["Nantuko Shade"] = new(ManaCost.Parse("{B}{B}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Insect", "Shade"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{B}")),
                    new PumpSelfEffect(1, 1))],
            },
            ["Ravenous Rats"] = new(ManaCost.Parse("{1}{B}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Rat"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new OpponentDiscardsEffect(1))],
            },
            ["Graveborn Muse"] = new(ManaCost.Parse("{2}{B}{B}"), null, 3, 3, CardType.Creature)
            {
                Subtypes = ["Zombie", "Spirit"],
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep,
                    new DynamicDrawAndLoseLifeEffect(
                        p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Zombie", StringComparer.OrdinalIgnoreCase))))],
            },
            ["Spawning Pool"] = new(null, ManaAbility.Fixed(ManaColor.Black), null, null, CardType.Land)
            {
                EntersTapped = true,
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{B}")), new BecomeCreatureEffect(1, 1, "Skeleton"))],
            },
            ["Skeletal Scrying"] = new(ManaCost.Parse("{0}{B}"), null, null, null, CardType.Instant,
                Effect: new SkeletalScryingEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },

            // === Deadguy Ale deck ===
            ["Exalted Angel"] = new(ManaCost.Parse("{4}{W}{W}"), null, 4, 5, CardType.Creature)
            {
                Subtypes = ["Angel"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Exalted Angel",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Exalted Angel",
                        GrantedKeyword: Keyword.Lifelink,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Knight of Stromgald"] = new(ManaCost.Parse("{B}{B}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Knight"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Knight of Stromgald",
                        GrantedKeyword: Keyword.ProtectionFromWhite,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{B}{B}")), new PumpSelfEffect(1, 0))],
            },
            ["Phyrexian Rager"] = new(ManaCost.Parse("{2}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Phyrexian", "Horror"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawAndLoseLifeEffect(1, 1))],
            },
            ["Phyrexian Arena"] = new(ManaCost.Parse("{1}{B}{B}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new DrawAndLoseLifeEffect(1, 1))],
            },

            // === Landstill deck ===
            ["Prohibit"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CmcCheckCounterEffect(2))
            {
                SpellRole = SpellRole.Counterspell,
            },
            ["Standstill"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.AnyPlayerCastsSpell, new StandstillEffect())],
            },
            ["Humility"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.RemoveAbilities,
                        (card, _) => card.IsCreature,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.SetBasePowerToughness,
                        (card, _) => card.IsCreature,
                        SetPower: 1, SetToughness: 1,
                        Layer: EffectLayer.Layer7b_SetPT),
                ],
            },
            ["Decree of Justice"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Sorcery,
                Effect: new DecreeOfJusticeEffect())
            {
                CyclingCost = ManaCost.Parse("{2}{W}"),
                CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new DecreeOfJusticeCyclingEffect())],
            },
            ["Phyrexian Furnace"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{1}")),
                    new PhyrexianFurnaceEffect())],
            },
            ["Faerie Conclave"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land)
            {
                EntersTapped = true,
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{U}")), new BecomeCreatureEffect(2, 1, [Keyword.Flying], "Faerie"))],
            },
            ["Mishra's Factory"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")), new BecomeCreatureEffect(2, 2, "Assembly-Worker"))],
            },

            // === Oath of Druids deck ===
            ["Terravore"] = new(ManaCost.Parse("{1}{G}{G}"), null, null, null, CardType.Creature)
            {
                Subtypes = ["Lhurgoyf"],
                DynamicBasePower = state => CountLandsInAllGraveyards(state),
                DynamicBaseToughness = state => CountLandsInAllGraveyards(state),
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Terravore",
                        GrantedKeyword: Keyword.Trample,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Call of the Herd"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Sorcery,
                Effect: new CreateTokenSpellEffect("Elephant", 3, 3, CardType.Creature, ["Elephant"], tokenColors: [ManaColor.Green]))
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{3}{G}")),
            },
            ["Cataclysm"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Sorcery,
                Effect: new CataclysmEffect()),
            ["Oath of Druids"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.AnyUpkeep, new OathOfDruidsEffect())],
            },
            ["Ray of Revelation"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Instant,
                TargetFilter.Enchantment(), new DestroyTargetSpellEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{G}")),
                SpellRole = SpellRole.InstantRemoval,
            },
            ["Reckless Charge"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.Creature(), new RecklessChargeEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{2}{R}")),
            },
            ["Volcanic Spray"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DamageNonflyingCreaturesAndPlayersEffect(1))
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{1}{R}")),
            },
            ["Quiet Speculation"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Sorcery,
                Effect: new QuietSpeculationEffect()),
            ["Funeral Pyre"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                Effect: new FuneralPyreEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },
            ["Treetop Village"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land)
            {
                EntersTapped = true,
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{G}")), new BecomeCreatureEffect(3, 3, [Keyword.Trample], "Ape"))],
            },

            // === Terrageddon deck ===
            ["Mother of Runes"] = new(ManaCost.Parse("{W}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Cleric"],
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true),
                    new GrantProtectionEffect(),
                    TargetFilter: c => c.IsCreature,
                    TargetOwnOnly: true)],
            },
            ["Nimble Mongoose"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Mongoose"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Nimble Mongoose",
                        GrantedKeyword: Keyword.Shroud,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                        (card, player) => card.Name == "Nimble Mongoose" && player.Graveyard.Count >= 7,
                        PowerMod: 2, ToughnessMod: 2,
                        Layer: EffectLayer.Layer7c_ModifyPT),
                ],
            },
            ["Zuran Orb"] = new(ManaCost.Parse("{0}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeCardType: CardType.Land),
                    new Triggers.Effects.GainLifeEffect(2))],
            },

            // === Elves deck ===
            ["Llanowar Elves"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true), new AddManaEffect(ManaColor.Green))],
            },
            ["Fyndhorn Elves"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true), new AddManaEffect(ManaColor.Green))],
            },
            ["Priest of Titania"] = new(ManaCost.Parse("{1}{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true),
                    new DynamicAddManaEffect(ManaColor.Green,
                        state => state.Player1.Battlefield.Cards
                            .Concat(state.Player2.Battlefield.Cards)
                            .Count(c => c.Subtypes.Contains("Elf", StringComparer.OrdinalIgnoreCase))))],
            },
            ["Quirion Ranger"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature) { Subtypes = ["Elf", "Ranger"] },
            ["Wirewood Symbiote"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature) { Subtypes = ["Insect"] },
            ["Multani's Acolyte"] = new(ManaCost.Parse("{G}{G}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Elf"],
                EchoCost = ManaCost.Parse("{G}{G}"),
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawCardEffect())],
            },
            ["Deranged Hermit"] = new(ManaCost.Parse("{3}{G}{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf"],
                EchoCost = ManaCost.Parse("{3}{G}{G}"),
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Squirrel", 1, 1, CardType.Creature, ["Squirrel"], count: 4, tokenColors: [ManaColor.Green]))],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                        (card, _) => card.Subtypes.Contains("Squirrel"),
                        PowerMod: 1, ToughnessMod: 1,
                        Layer: EffectLayer.Layer7c_ModifyPT),
                ],
            },
            ["Wall of Blossoms"] = new(ManaCost.Parse("{1}{G}"), null, 0, 4, CardType.Creature)
            {
                Subtypes = ["Plant", "Wall"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawCardEffect())],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Wall of Blossoms",
                        GrantedKeyword: Keyword.Defender,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Wall of Roots"] = new(ManaCost.Parse("{1}{G}"), null, 0, 5, CardType.Creature)
            {
                Subtypes = ["Plant", "Wall"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Wall of Roots",
                        GrantedKeyword: Keyword.Defender,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Ravenous Baloth"] = new(ManaCost.Parse("{2}{G}{G}"), null, 4, 4, CardType.Creature)
            {
                Subtypes = ["Beast"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(SacrificeSubtype: "Beast"),
                    new Triggers.Effects.GainLifeEffect(4))],
            },
            ["Caller of the Claw"] = new(ManaCost.Parse("{2}{G}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Elf"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Caller of the Claw",
                        GrantedKeyword: Keyword.Flash,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CallerOfTheClawEffect())],
            },
            ["Masticore"] = new(ManaCost.Parse("{4}"), null, 4, 4, CardType.Artifact | CardType.Creature)
            {
                Subtypes = ["Masticore"],
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new MasticoreUpkeepEffect())],
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{2}")),
                    new DealDamageEffect(1),
                    TargetFilter: c => c.IsCreature)],
            },
            ["Nantuko Vigilante"] = new(ManaCost.Parse("{3}{G}"), null, 3, 2, CardType.Creature) { Subtypes = ["Insect", "Druid", "Mutant"] },
            ["Yavimaya Granger"] = new(ManaCost.Parse("{2}{G}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Elf"],
                EchoCost = ManaCost.Parse("{2}{G}"),
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new SearchLandToBattlefieldEffect(c => c.IsBasicLand, entersTapped: true))],
            },
            ["Anger"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Incarnation"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Anger",
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                GraveyardAbilities =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, player) => card.IsCreature
                            && player.Battlefield.Cards.Any(c =>
                                c.Name == "Mountain"
                                || c.Subtypes.Contains("Mountain", StringComparer.OrdinalIgnoreCase)),
                        GrantedKeyword: Keyword.Haste,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Squee, Goblin Nabob"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                IsLegendary = true,
                Triggers = [new Trigger(GameEvent.Upkeep,
                    TriggerCondition.SelfInGraveyardDuringUpkeep,
                    new ReturnSelfFromGraveyardEffect())],
            },
            ["Survival of the Fittest"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{G}"), DiscardCardType: CardType.Creature),
                    new SearchLibraryEffect(c => c.CardTypes.HasFlag(CardType.Creature), "Creature"))],
            },
            ["Gaea's Cradle"] = new(null, ManaAbility.Dynamic(ManaColor.Green,
                p => p.Battlefield.Cards.Count(c => c.IsCreature)),
                null, null, CardType.Land) { IsLegendary = true },

            // ─── Legacy Dimir Tempo ────────────────────────────────────────────

            ["Orcish Bowmasters"] = new(ManaCost.Parse("{1}{B}"), null, 1, 1, CardType.Creature)
            {
                HasFlash = true,
                Subtypes = ["Orc", "Archer"],
                Triggers =
                [
                    new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new BowmastersEffect()),
                    new Trigger(GameEvent.DrawCard, TriggerCondition.OpponentDrawsExceptFirst, new BowmastersEffect()),
                ],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Orcish Bowmasters",
                        GrantedKeyword: Keyword.Flash,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },

            // ─── Legacy Sneak and Show ──────────────────────────────────────────

            ["Show and Tell"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Sorcery,
                Effect: new ShowAndTellEffect()),

            ["Sneak Attack"] = new(ManaCost.Parse("{3}{R}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{R}")),
                    new SneakAttackPutEffect())],
            },

            ["Emrakul, the Aeons Torn"] = new(ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature)
            {
                IsLegendary = true,
                Subtypes = ["Eldrazi"],
                ShuffleGraveyardOnDeath = true,
                CannotBeCountered = true,
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Emrakul, the Aeons Torn",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Emrakul, the Aeons Torn",
                        GrantedKeyword: Keyword.ProtectionFromColoredSpells,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers =
                [
                    new Trigger(GameEvent.SpellCast, TriggerCondition.SelfIsCast, new ExtraTurnEffect()),
                    new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new AnnihilatorEffect(6)),
                ],
            },

            ["Griselbrand"] = new(ManaCost.Parse("{4}{B}{B}{B}{B}"), null, 7, 7, CardType.Creature)
            {
                IsLegendary = true,
                Subtypes = ["Demon"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Griselbrand",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Griselbrand",
                        GrantedKeyword: Keyword.Lifelink,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(PayLife: 7),
                    new DrawCardsActivatedEffect(7))],
            },

            ["Lotus Petal"] = new(ManaCost.Parse("{0}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true),
                    new AddAnyManaEffect())],
            },

            ["Spell Pierce"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.NoncreatureSpell(), new ConditionalCounterEffect(2))
            {
                SpellRole = SpellRole.Counterspell,
            },

            ["Ancient Tomb"] = new(null, ManaAbility.FixedMultiple(ManaColor.Colorless, 2, selfDamage: 2),
                null, null, CardType.Land),

            ["City of Traitors"] = new(null, ManaAbility.FixedMultiple(ManaColor.Colorless, 2),
                null, null, CardType.Land)
            {
                Triggers =
                [
                    new Trigger(GameEvent.LandPlayed, TriggerCondition.ControllerPlaysAnotherLand,
                        new SacrificeSelfOnLandEffect()),
                ],
            },

            ["Intuition"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Instant,
                Effect: new IntuitionEffect()),

            // ─── Sneak and Show Sideboard ─────────────────────────────────────────

            ["Blood Moon"] = new(ManaCost.Parse("{2}{R}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.OverrideLandType,
                        (card, _) => card.IsLand && !card.IsBasicLand,
                        Layer: EffectLayer.Layer4_TypeChanging),
                ],
            },

            ["Pyroclasm"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DamageAllCreaturesEffect(2)),

            ["Flusterstorm"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.InstantOrSorcerySpell(), new ConditionalCounterEffect(1))
            {
                SpellRole = SpellRole.Counterspell,
            },

            ["Pyroblast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.SpellOrPermanent(), new PyroblastEffect())
            {
                SpellRole = SpellRole.Counterspell,
            },

            ["Surgical Extraction"] = new(ManaCost.Parse("{B/P}"), null, null, null, CardType.Instant,
                Effect: new SurgicalExtractionEffect())
            {
                SpellRole = SpellRole.InstantUtility,
            },

            ["Dismember"] = new(ManaCost.Parse("{1}{B/P}{B/P}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new PumpTargetEffect(-5, -5))
            {
                SpellRole = SpellRole.InstantRemoval,
            },

            ["Grafdigger's Cage"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventCastFromGraveyard,
                        (_, _) => true),
                ],
            },

            ["Wipe Away"] = new(ManaCost.Parse("{1}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.AnyPermanent(), new BounceTargetEffect())
            {
                SpellRole = SpellRole.InstantRemoval,
            },

            // ─── Planeswalkers ──────────────────────────────────────────────────

            ["Kaito, Bane of Nightmares"] = new(ManaCost.Parse("{2}{U}{B}"), null, null, null, CardType.Planeswalker)
            {
                IsLegendary = true,
                Subtypes = ["Kaito", "Ninja"],
                StartingLoyalty = 4,
                NinjutsuCost = ManaCost.Parse("{1}{U}{B}"),
                LoyaltyAbilities =
                [
                    new LoyaltyAbility(1, new CreateNinjaEmblemEffect(),
                        "+1: You get an emblem with \"Ninjas you control get +1/+1.\""),
                    new LoyaltyAbility(0, new SurveilAndDrawEffect(),
                        "0: Surveil 2. Then draw a card for each opponent who lost life this turn."),
                    new LoyaltyAbility(-2, new TapAndStunEffect(),
                        "-2: Tap target creature. Put two stun counters on it."),
                ],
                ContinuousEffects =
                [
                    // During your turn, as long as Kaito has 1+ loyalty counters,
                    // he's a 3/4 Ninja creature with hexproof.
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.BecomeCreature,
                        (card, _) => card.Name == "Kaito, Bane of Nightmares"
                            && card.GetCounters(CounterType.Loyalty) > 0,
                        SetPower: 3, SetToughness: 4,
                        ApplyToSelf: true,
                        Layer: EffectLayer.Layer4_TypeChanging,
                        StateCondition: state =>
                            state.ActivePlayer.Battlefield.Cards.Any(
                                c => c.Name == "Kaito, Bane of Nightmares")),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Kaito, Bane of Nightmares"
                            && card.GetCounters(CounterType.Loyalty) > 0,
                        GrantedKeyword: Keyword.Hexproof,
                        ApplyToSelf: true,
                        Layer: EffectLayer.Layer6_AbilityAddRemove,
                        StateCondition: state =>
                            state.ActivePlayer.Battlefield.Cards.Any(
                                c => c.Name == "Kaito, Bane of Nightmares")),
                ],
            },

            ["Tamiyo, Inquisitive Student"] = new(ManaCost.Parse("{U}"), null, 0, 3, CardType.Creature)
            {
                Name = "Tamiyo, Inquisitive Student",
                IsLegendary = true,
                Subtypes = ["Moonfolk", "Wizard"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Tamiyo, Inquisitive Student",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove,
                        ApplyToSelf: true),
                ],
                Triggers =
                [
                    new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new InvestigateEffect()),
                    new Trigger(GameEvent.DrawCard, TriggerCondition.ThirdDrawInTurn, new TransformExileReturnEffect()),
                ],
                TransformInto = new CardDefinition(null, null, null, null, CardType.Planeswalker)
                {
                    Name = "Tamiyo, Seasoned Scholar",
                    IsLegendary = true,
                    Subtypes = ["Tamiyo"],
                    StartingLoyalty = 2,
                    LoyaltyAbilities =
                    [
                        new LoyaltyAbility(2, new TamiyoDefenseEffect(),
                            "+2: Creatures attacking you get -1/-0 until next turn"),
                        new LoyaltyAbility(-3, new TamiyoRecoverEffect(),
                            "-3: Return instant or sorcery from graveyard to hand"),
                        new LoyaltyAbility(-7, new TamiyoUltimateEffect(),
                            "-7: Draw half library, emblem no max hand size"),
                    ],
                },
            },

            ["Polluted Delta"] = new(null, null, null, null, CardType.Land)
            { Name = "Polluted Delta", FetchAbility = new FetchAbility(["Island", "Swamp"]) },

            ["Underground Sea"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Black),
                null, null, CardType.Land)
            { Name = "Underground Sea", Subtypes = ["Island", "Swamp"] },

            ["Misty Rainforest"] = new(null, null, null, null, CardType.Land)
            { Name = "Misty Rainforest", FetchAbility = new FetchAbility(["Forest", "Island"]) },

            ["Undercity Sewers"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Black),
                null, null, CardType.Land)
            {
                Name = "Undercity Sewers",
                Subtypes = ["Island", "Swamp"],
                EntersTapped = true,
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SurveilEffect(1))],
            },

            ["Thoughtseize"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new ThoughtseizeEffect())
            { Name = "Thoughtseize" },

            ["Fatal Push"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new FatalPushEffect())
            {
                Name = "Fatal Push",
                SpellRole = SpellRole.InstantRemoval,
            },

            ["Brazen Borrower"] = new(ManaCost.Parse("{1}{U}{U}"), null, 3, 1, CardType.Creature)
            {
                Name = "Brazen Borrower",
                Subtypes = ["Faerie", "Rogue"],
                HasFlash = true,
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Brazen Borrower",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove, ApplyToSelf: true),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Brazen Borrower",
                        GrantedKeyword: Keyword.CantBlockNonFlying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove, ApplyToSelf: true),
                ],
                Adventure = new AdventurePart("Petty Theft", ManaCost.Parse("{1}{U}"),
                    Filter: TargetFilter.NonlandPermanent(),
                    Effect: new PettyTheftEffect()),
            },

            // ─── Premodern Missing Cards ─────────────────────────────────────────

            // Lands
            ["Yavimaya Coast"] = new(null, ManaAbility.PainChoice(
                [ManaColor.Colorless, ManaColor.Green, ManaColor.Blue],
                [ManaColor.Green, ManaColor.Blue]), null, null, CardType.Land),

            // Vanilla creatures
            ["Savannah Lions"] = new(ManaCost.Parse("{W}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Cat"],
            },

            // Removal spells
            ["Red Elemental Blast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.SpellOrPermanent(), new PyroblastEffect()),
            ["Blue Elemental Blast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.SpellOrPermanent(), new BlueElementalBlastEffect()),
            ["Hydroblast"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.SpellOrPermanent(), new BlueElementalBlastEffect()),
            ["Annul"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.ArtifactOrEnchantmentSpell(), new CounterSpellEffect()),
            ["Erase"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                TargetFilter.Enchantment(), new ExileEnchantmentEffect()),
            ["Perish"] = new(ManaCost.Parse("{2}{B}"), null, null, null, CardType.Sorcery,
                Effect: new DestroyAllByColorEffect(ManaColor.Green, CardType.Creature)),
            ["Anarchy"] = new(ManaCost.Parse("{2}{R}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DestroyAllByColorEffect(ManaColor.White)),
            // NOTE: Oracle targets opponent — functionally identical in 2-player game
            ["Simoon"] = new(ManaCost.Parse("{R}{G}"), null, null, null, CardType.Instant,
                Effect: new DamageOpponentCreaturesEffect(1)),
            ["Crumble"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Instant,
                TargetFilter.Artifact(), new CrumbleEffect()),
            ["Tranquil Domain"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
                Effect: new DestroyAllNonAuraEnchantmentsEffect()),

            // Draw/Filter spells
            ["Careful Study"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                Effect: new CarefulStudyEffect()),
            ["Peek"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new PeekEffect()),
            ["Accumulated Knowledge"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                Effect: new AccumulatedKnowledgeEffect()),
            ["Portent"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new PortentEffect()),
            ["Enlightened Tutor"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                Effect: new EnlightenedTutorEffect()),
            ["Frantic Search"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Instant,
                Effect: new FranticSearchEffect()),
            ["Price of Progress"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Instant,
                Effect: new PriceOfProgressEffect()),
            ["Earthquake"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                Effect: new EarthquakeEffect()),

            // Static enchantments & artifacts
            ["Crusade"] = new(ManaCost.Parse("{W}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                        (card, _) => card.IsCreature && card.Colors.Contains(ManaColor.White),
                        PowerMod: 1, ToughnessMod: 1,
                        Layer: EffectLayer.Layer7c_ModifyPT),
                ],
            },
            ["Absolute Law"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.IsCreature,
                        GrantedKeyword: Keyword.ProtectionFromRed,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Worship"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventLethalDamage,
                        (_, _) => true),
                ],
            },
            ["Sphere of Resistance"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: 1,
                        CostApplies: _ => true),
                ],
            },
            ["Chill"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: 2,
                        CostApplies: c => c.Colors.Contains(ManaColor.Red)),
                ],
            },
            ["Gloom"] = new(ManaCost.Parse("{2}{B}"), null, null, null, CardType.Enchantment)
            {
                ContinuousEffects =
                [
                    // White spells cost {3} more to cast
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
                        (_, _) => true, CostMod: 3,
                        CostApplies: c => c.Colors.Contains(ManaColor.White)),
                    // Activated abilities of white enchantments cost {3} more to activate
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyActivatedAbilityCost,
                        (_, _) => true, CostMod: 3,
                        ActivatedAbilityCostApplies: c =>
                            c.CardTypes.HasFlag(CardType.Enchantment)
                            && c.Colors.Contains(ManaColor.White)),
                ],
            },
            ["Null Rod"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventActivatedAbilities,
                        (card, _) => card.CardTypes.HasFlag(CardType.Artifact)),
                ],
            },
            ["Cursed Totem"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
            {
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventActivatedAbilities,
                        (card, _) => card.IsCreature),
                ],
            },

            // Creatures with activated abilities
            ["True Believer"] = new(ManaCost.Parse("{W}{W}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Human", "Cleric"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantPlayerShroud, (_, _) => true),
                ],
            },
            ["Nova Cleric"] = new(ManaCost.Parse("{W}"), null, 1, 2, CardType.Creature)
            {
                Subtypes = ["Human", "Cleric"],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{2}{W}")),
                        new DestroyAllEnchantmentsEffect()),
                ],
            },
            ["Thornscape Apprentice"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Wizard"],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{R}")),
                        new GrantFirstStrikeEffect(), c => c.IsCreature),
                    new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{W}")),
                        new TapTargetEffect(), c => c.IsCreature),
                ],
            },
            ["Waterfront Bouncer"] = new(ManaCost.Parse("{1}{U}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Merfolk", "Spellshaper"],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{U}"), DiscardAny: true),
                        new BounceTargetCreatureEffect(), c => c.IsCreature),
                ],
            },
            ["Wild Mongrel"] = new(ManaCost.Parse("{1}{G}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Dog"],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(DiscardAny: true),
                        new WildMongrelEffect()),
                ],
            },
            ["Aquamoeba"] = new(ManaCost.Parse("{1}{U}"), null, 1, 3, CardType.Creature)
            {
                Subtypes = ["Elemental", "Beast"],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(DiscardAny: true),
                        new SwapPowerToughnessEffect()),
                ],
            },
            // === Madness Cards ===

            // Basking Rootwalla: {G} 1/1 Lizard. {1}{G}: +2/+2 until EOT (once per turn). Madness {0}.
            ["Basking Rootwalla"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Lizard"],
                MadnessCost = ManaCost.Parse("{0}"),
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{G}")),
                        new PumpSelfEffect(2, 2), OncePerTurn: true),
                ],
            },
            // Arrogant Wurm: {3}{G}{G} 4/4 Wurm. Trample. Madness {2}{G}.
            ["Arrogant Wurm"] = new(ManaCost.Parse("{3}{G}{G}"), null, 4, 4, CardType.Creature)
            {
                Subtypes = ["Wurm"],
                MadnessCost = ManaCost.Parse("{2}{G}"),
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Arrogant Wurm",
                        GrantedKeyword: Keyword.Trample,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            // Circular Logic: {2}{U} Instant. Counter target spell unless its controller pays {1} for each card in your graveyard. Madness {U}.
            ["Circular Logic"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Instant,
                TargetFilter.AnySpellOnStack(), new CircularLogicEffect())
            {
                MadnessCost = ManaCost.Parse("{U}"),
            },

            ["Flametongue Kavu"] = new(ManaCost.Parse("{3}{R}"), null, 4, 2, CardType.Creature)
            {
                Subtypes = ["Kavu"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DealDamageToTargetCreatureEffect(4))],
            },

            // === Enchantments with Triggers ===

            // Warmth: Whenever an opponent casts a red spell, you gain 2 life.
            ["Warmth"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.OpponentCastsRedSpell, new Triggers.Effects.GainLifeEffect(2))],
            },

            // Spiritual Focus: Whenever a spell or ability an opponent controls causes you to discard a card,
            // gain 2 life and you may draw a card.
            ["Spiritual Focus"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.DiscardCard, TriggerCondition.OpponentCausesControllerDiscard, new GainLifeAndOptionalDrawEffect(2))],
            },

            // Presence of the Master: Whenever a player casts an enchantment spell, counter it.
            ["Presence of the Master"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.AnyPlayerCastsEnchantment, new CounterSpellOnStackEffect())],
            },

            // Sacred Ground: Whenever a spell or ability an opponent controls causes a land to be put
            // into your graveyard from the battlefield, return that card to the battlefield.
            ["Sacred Ground"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.OpponentCausesControllerLandToGraveyard, new SacredGroundEffect())],
            },

            // Seal of Fire: Sacrifice: Deal 2 damage to any target.
            ["Seal of Fire"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(SacrificeSelf: true),
                        new DealDamageEffect(2),
                        TargetFilter: c => true,
                        CanTargetPlayer: true),
                ],
            },

            // Ivory Tower: At the beginning of your upkeep, gain X life where X = hand size - 4.
            ["Ivory Tower"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new IvoryTowerEffect())],
            },

            // Rejuvenation Chamber: Fading 2. {T}: You gain 2 life.
            ["Rejuvenation Chamber"] = new(ManaCost.Parse("{3}"), null, null, null, CardType.Artifact)
            {
                EntersWithCounters = new() { [CounterType.Fade] = 2 },
                Triggers =
                [
                    new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new FadingUpkeepEffect()),
                ],
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(TapSelf: true), new Triggers.Effects.GainLifeEffect(2)),
                ],
            },

            // Serenity: At the beginning of your upkeep, destroy all artifacts and enchantments.
            ["Serenity"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new SerenityEffect())],
            },

            // Carpet of Flowers: At the beginning of each of your main phases, add X mana of any color where X = opponent's Islands.
            ["Carpet of Flowers"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.MainPhaseBeginning, TriggerCondition.ControllerMainPhaseBeginning, new CarpetOfFlowersEffect())],
            },

            // Zombie Infestation: Discard 2 cards: Create a 2/2 black Zombie creature token.
            ["Zombie Infestation"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities =
                [
                    new(new ActivatedAbilityCost(DiscardCount: 2),
                        new CreateTokensEffect("Zombie", 2, 2, CardType.Creature, ["Zombie"], tokenColors: [ManaColor.Black])),
                ],
            },

            // === Alternate cost spells ===
            ["Gush"] = new(ManaCost.Parse("{4}{U}"), null, null, null, CardType.Instant,
                Effect: new DrawCardsEffect(2))
            {
                AlternateCost = new AlternateCost(ReturnLandSubtype: "Island", ReturnLandCount: 2),
            },
            ["Foil"] = new(ManaCost.Parse("{2}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect())
            {
                AlternateCost = new AlternateCost(DiscardLandSubtype: "Island", DiscardAnyCount: 1),
            },
            ["Spinning Darkness"] = new(ManaCost.Parse("{4}{B}{B}"), null, null, null, CardType.Instant,
                TargetFilter.NonBlackCreature(), new SpinningDarknessEffect())
            {
                AlternateCost = new AlternateCost(ExileFromGraveyardCount: 3, ExileFromGraveyardColor: ManaColor.Black),
            },
            ["Mogg Salvage"] = new(ManaCost.Parse("{2}{R}"), null, null, null, CardType.Instant,
                TargetFilter.Artifact(), new DestroyTargetSpellEffect())
            {
                AlternateCost = new AlternateCost(RequiresControlSubtype: "Mountain", RequiresOpponentSubtype: "Island"),
            },
            ["Pyrokinesis"] = new(ManaCost.Parse("{4}{R}{R}"), null, null, null, CardType.Instant,
                Effect: new DividedDamageEffect(4))
            {
                AlternateCost = new AlternateCost(ExileCardColor: ManaColor.Red),
            },
            ["Gaea's Blessing"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new GaeasBlessingEffect())
            {
                ShuffleGraveyardOnMill = true,
            },

            // --- Flashback + Echo cards ---
            ["Roar of the Wurm"] = new(ManaCost.Parse("{6}{G}"), null, null, null, CardType.Sorcery,
                Effect: new CreateTokenSpellEffect("Wurm", 6, 6, CardType.Creature, ["Wurm"], tokenColors: [ManaColor.Green]))
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{3}{G}")),
            },
            ["Krosan Reclamation"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new KrosanReclamationEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{1}{G}")),
            },
            ["Flash of Insight"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                Effect: new FlashOfInsightEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost: ManaCost.Parse("{1}{U}"), ExileBlueCardsFromGraveyard: 1),
            },
            ["Radiant's Dragoons"] = new(ManaCost.Parse("{3}{W}"), null, 2, 5, CardType.Creature)
            {
                Subtypes = ["Human", "Soldier"],
                EchoCost = ManaCost.Parse("{3}{W}"),
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new Triggers.Effects.GainLifeEffect(5))],
            },
            ["Attunement"] = new(ManaCost.Parse("{2}{U}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(
                    new ActivatedAbilityCost(ReturnSelfToHand: true),
                    new AttunementEffect())],
            },

            // Shadow creatures
            ["Dauthi Slayer"] = new(ManaCost.Parse("{B}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Dauthi", "Soldier"],
                MustAttack = true,
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Dauthi Slayer",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Soltari Foot Soldier"] = new(ManaCost.Parse("{W}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Soltari", "Soldier"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Foot Soldier",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Soltari Monk"] = new(ManaCost.Parse("{W}{W}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Soltari", "Monk", "Cleric"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Monk",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Monk",
                        GrantedKeyword: Keyword.ProtectionFromBlack,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Soltari Priest"] = new(ManaCost.Parse("{W}{W}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Soltari", "Cleric"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Priest",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Priest",
                        GrantedKeyword: Keyword.ProtectionFromRed,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },
            ["Soltari Champion"] = new(ManaCost.Parse("{2}{W}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Soltari", "Soldier"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Soltari Champion",
                        GrantedKeyword: Keyword.Shadow,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new PumpAllOtherCreaturesEffect(1, 1))],
            },
            ["Xantid Swarm"] = new(ManaCost.Parse("{G}"), null, 0, 1, CardType.Creature)
            {
                Subtypes = ["Insect"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Xantid Swarm",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.BeginCombat, TriggerCondition.SelfAttacks, new XantidSwarmEffect())],
            },

            // === Choose Name/Type + Hate Cards ===
            ["Engineered Plague"] = new(ManaCost.Parse("{2}{B}"), null, null, null, CardType.Enchantment)
            {
                // "As Engineered Plague enters the battlefield, choose a creature type."
                // This is a replacement effect, not a trigger — cannot be Stifled.
                AsEntersBattlefieldEffect = new ChooseCreatureTypeEffect(),
                // ContinuousEffect is generated dynamically via DynamicContinuousEffectsFactory
                // because the -1/-1 effect depends on the runtime ChosenType
                DynamicContinuousEffectsFactory = card =>
                {
                    if (string.IsNullOrEmpty(card.ChosenType))
                        return [];
                    var chosenType = card.ChosenType;
                    return
                    [
                        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                            (target, _) => target.IsCreature
                                && target.Subtypes.Contains(chosenType, StringComparer.OrdinalIgnoreCase),
                            PowerMod: -1, ToughnessMod: -1,
                            Layer: EffectLayer.Layer7c_ModifyPT),
                    ];
                },
            },
            ["Meddling Mage"] = new(ManaCost.Parse("{W}{U}"), null, 2, 2, CardType.Creature)
            {
                IsLegendary = false,
                Subtypes = ["Human", "Wizard"],
                // "As Meddling Mage enters the battlefield, choose a nonland card name."
                // This is a replacement effect, not a trigger — cannot be Stifled.
                AsEntersBattlefieldEffect = new ChooseCardNameEffect(),
                // Cast prevention is enforced in CastSpellHandler by checking ChosenName on Meddling Mages
            },
            ["Ensnaring Bridge"] = new(ManaCost.Parse("{3}"), null, null, null, CardType.Artifact)
            {
                // Attack restriction is enforced in GameEngine.RunCombatAsync
                // Creatures with power > cards in controller's hand can't attack
            },
            ["Tsabo's Web"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawCardEffect())],
                ContinuousEffects =
                [
                    // "Each land with an activated ability that isn't a mana ability
                    // doesn't untap during its controller's untap step."
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) =>
                        {
                            if (!card.IsLand) return false;
                            if (!CardDefinitions.TryGet(card.Name, out var def)) return false;
                            // FetchAbility is a non-mana activated ability
                            if (def.FetchAbility != null) return true;
                            // ActivatedAbilities are non-mana activated abilities
                            if (def.ActivatedAbilities.Count > 0) return true;
                            return false;
                        },
                        GrantedKeyword: Keyword.DoesNotUntap,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },

            // ─── Task 12 Batch 1: Complex Cards ─────────────────────────────────

            // Kicker cards
            ["Overload"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.Artifact(), new Effects.OverloadEffect())
            {
                KickerCost = ManaCost.Parse("{2}"),
            },
            ["Orim's Chant"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new Effects.OrimsChantEffect())
            {
                KickerCost = ManaCost.Parse("{W}"),
            },

            // Rancor (Aura with return-to-hand trigger)
            ["Rancor"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment)
            {
                Subtypes = ["Aura"],
                AuraTarget = AuraTarget.Creature,
                DynamicContinuousEffectsFactory = rancorCard =>
                {
                    if (!rancorCard.AttachedTo.HasValue) return [];
                    var attachedId = rancorCard.AttachedTo.Value;
                    return
                    [
                        new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
                            (card, _) => card.Id == attachedId,
                            PowerMod: 2, ToughnessMod: 0,
                            Layer: EffectLayer.Layer7c_ModifyPT),
                        new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                            (card, _) => card.Id == attachedId,
                            GrantedKeyword: Keyword.Trample,
                            Layer: EffectLayer.Layer6_AbilityAddRemove),
                    ];
                },
                Triggers = [new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.SelfLeavesBattlefield, new RancorReturnEffect())],
            },

            // Stifle
            ["Stifle"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                Effect: new Effects.StifleEffect()),



            // Brain Freeze (storm)
            ["Brain Freeze"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new Effects.BrainFreezeEffect())
            {
                HasStorm = true,
            },

            // River Boa (islandwalk + regeneration)
            ["River Boa"] = new(ManaCost.Parse("{1}{G}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Snake"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "River Boa",
                        GrantedKeyword: Keyword.Islandwalk,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{G}")), new RegenerateEffect())],
            },

            // Circle of Protection: Red
            ["Circle of Protection: Red"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")),
                    new CoPPreventDamageEffect(ManaColor.Red))],
            },

            // Circle of Protection: Black
            ["Circle of Protection: Black"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbilities = [new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")),
                    new CoPPreventDamageEffect(ManaColor.Black))],
            },

            // === Task 12 Batch 2: Final 9 complex cards ===

            // Assault (split card half: {R} sorcery, deal 2 damage to any target)
            ["Assault"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter: TargetFilter.CreatureOrPlayer(),
                Effect: new DamageEffect(2)),

            // Battery (split card half: {3}{G} sorcery, create 3/3 Elephant token)
            ["Battery"] = new(ManaCost.Parse("{3}{G}"), null, null, null, CardType.Sorcery,
                Effect: new BatteryEffect()),

            // Ramosian Sergeant ({W}, 1/1 Human Rebel, search for Rebel CMC 2 or less to battlefield)
            ["Ramosian Sergeant"] = new(ManaCost.Parse("{W}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Rebel"],
                ActivatedAbilities = [new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{3}")),
                    new SearchLibraryEffect(c => c.Subtypes.Contains("Rebel", StringComparer.OrdinalIgnoreCase) && (c.ManaCost?.ConvertedManaCost ?? 0) <= 2, "Rebel (CMC 2 or less)", SearchDestination.Battlefield))],
            },

            // Eternal Dragon ({5}{W}{W}, 5/5 Dragon Spirit, Flying, Plainscycling {2}, graveyard return)
            // NOTE: Oracle "{3}{W}{W}: Return Eternal Dragon from your graveyard to your hand. Activate only during your upkeep."
            // is an activated ability, not a trigger. Modeled as an upkeep trigger with optional payment since the engine
            // doesn't support graveyard-based activated abilities. Functionally equivalent — player gets the choice.
            ["Eternal Dragon"] = new(ManaCost.Parse("{5}{W}{W}"), null, 5, 5, CardType.Creature)
            {
                Subtypes = ["Dragon", "Spirit"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Eternal Dragon",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                CyclingCost = ManaCost.Parse("{2}"),
                CyclingReplaceDraw = true,
                CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new PlainscyclingEffect("Plains"))],
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.SelfInGraveyardDuringUpkeep,
                    new ReturnSelfForManaEffect(ManaCost.Parse("{3}{W}{W}")))],
            },

            // Phyrexian Dreadnought ({1}, 12/12 Artifact Creature, Trample, ETB sacrifice)
            ["Phyrexian Dreadnought"] = new(ManaCost.Parse("{1}"), null, 12, 12, CardType.Artifact | CardType.Creature)
            {
                Subtypes = ["Phyrexian", "Dreadnought"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Phyrexian Dreadnought",
                        GrantedKeyword: Keyword.Trample,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DreadnoughtETBEffect())],
            },

            // Decree of Silence ({6}{U}{U}, Enchantment)
            ["Decree of Silence"] = new(ManaCost.Parse("{6}{U}{U}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.OpponentCastsAnySpell, new DecreeOfSilenceEffect())],
                CyclingCost = ManaCost.Parse("{4}{U}{U}"),
                CyclingReplaceDraw = true,
                CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new CounterTopSpellEffect())],
            },

            // Dystopia ({1}{B}{B}, Enchantment, cumulative upkeep + sacrifice green/white)
            ["Dystopia"] = new(ManaCost.Parse("{1}{B}{B}"), null, null, null, CardType.Enchantment)
            {
                Triggers =
                [
                    new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new DystopiaUpkeepEffect()),
                    new Trigger(GameEvent.Upkeep, TriggerCondition.AnyUpkeep, new DystopiaSacrificeEffect()),
                ],
            },

            // Cleansing Meditation ({1}{W}{W}, Sorcery)
            ["Cleansing Meditation"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Sorcery,
                Effect: new CleansingMeditationEffect()),

            // Wonder ({3}{U}, 2/2 Incarnation, Flying, graveyard flying grant)
            ["Wonder"] = new(ManaCost.Parse("{3}{U}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Incarnation"],
                ContinuousEffects =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Wonder",
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                GraveyardAbilities =
                [
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, player) => card.IsCreature
                            && player.Graveyard.Cards.Any(g => g.Name == "Wonder")
                            && player.Battlefield.Cards.Any(l => l.Subtypes.Contains("Island")),
                        GrantedKeyword: Keyword.Flying,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
            },

            // Kirtar's Desire ({W}, Enchantment — Aura, prevents attacking; threshold: also prevents blocking)
            ["Kirtar's Desire"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Enchantment)
            {
                Subtypes = ["Aura"],
                AuraTarget = AuraTarget.Creature,
                DynamicContinuousEffectsFactory = aura =>
                {
                    var effects = new List<ContinuousEffect>();
                    if (aura.AttachedTo.HasValue)
                    {
                        var attachedId = aura.AttachedTo.Value;
                        // Can't attack
                        effects.Add(new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventCreatureAttacks,
                            (card, _) => card.Id == attachedId,
                            Layer: EffectLayer.Layer6_AbilityAddRemove));
                        // Can't block (threshold — controller of aura has 7+ cards in graveyard)
                        effects.Add(new ContinuousEffect(Guid.Empty, ContinuousEffectType.PreventCreatureBlocking,
                            (card, _) => card.Id == attachedId,
                            Layer: EffectLayer.Layer6_AbilityAddRemove,
                            StateCondition: state =>
                            {
                                // Find the aura's controller — check which player has it on battlefield
                                var p1HasAura = state.Player1.Battlefield.Cards.Any(c => c.Id == aura.Id);
                                var controller = p1HasAura ? state.Player1 : state.Player2;
                                return controller.Graveyard.Cards.Count >= 7;
                            }));
                    }
                    return effects;
                },
            },
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

    private static int CountLandsInAllGraveyards(GameState state)
    {
        return state.Player1.Graveyard.Cards.Count(c => c.IsLand)
             + state.Player2.Graveyard.Cards.Count(c => c.IsLand);
    }
}
