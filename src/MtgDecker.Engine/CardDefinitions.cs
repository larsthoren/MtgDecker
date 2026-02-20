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
                Subtypes = ["Goblin"],
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
                Subtypes = ["Goblin"],
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
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true), new DealDamageEffect(1), c => c.IsCreature, CanTargetPlayer: true),
            },
            ["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature)
            {
                Subtypes = ["Goblin"],
                CyclingCost = ManaCost.Parse("{1}{R}"),
                CyclingTriggers = [new Trigger(GameEvent.Cycle, TriggerCondition.Self, new GempalmIncineratorEffect())],
            },
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
                        PowerMod: 1, ToughnessMod: 1,
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
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSelf: true, ManaCost: ManaCost.Parse("{R}")), new DestroyTargetEffect(), c => c.CardTypes.HasFlag(CardType.Artifact)),
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
            ["Karplusan Forest"] = new(null, ManaAbility.PainChoice(
                [ManaColor.Colorless, ManaColor.Red, ManaColor.Green],
                [ManaColor.Red, ManaColor.Green]), null, null, CardType.Land),
            ["Wooded Foothills"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Mountain", "Forest"]) },
            ["Rishadan Port"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{1}")), new TapTargetEffect(), c => c.IsLand),
            },
            ["Wasteland"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true), new DestroyTargetEffect(), c => c.IsLand && !c.IsBasicLand),
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
                TargetFilter.Creature(), new SwordsToPlowsharesEffect()),
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
                    new Trigger(GameEvent.LeavesBattlefield, TriggerCondition.SelfLeavesBattlefield, new ReturnExiledCardsEffect()),
                ],
                ActivatedAbility = new(
                    new ActivatedAbilityCost(RemoveCounterType: CounterType.Fade),
                    new ExileCreatureEffect(),
                    c => c.IsCreature),
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
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Chain Lightning"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Lava Spike"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new DamageEffect(3, canTargetCreature: false)),
            ["Rift Bolt"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Fireblast"] = new(ManaCost.Parse("{4}{R}{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(4))
            {
                AlternateCost = new AlternateCost(SacrificeLandSubtype: "Mountain", SacrificeLandCount: 2),
            },
            ["Goblin Guide"] = new(ManaCost.Parse("{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Goblin"],
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
                TargetFilter.Spell(), new CounterSpellEffect())
            {
                AlternateCost = new AlternateCost(ReturnLandSubtype: "Island"),
            },
            ["Force of Will"] = new(ManaCost.Parse("{3}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterSpellEffect())
            {
                AlternateCost = new AlternateCost(LifeCost: 1, ExileCardColor: ManaColor.Blue),
            },
            ["Delver of Secrets"] = new(ManaCost.Parse("{U}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Wizard"] },
            ["Murktide Regent"] = new(ManaCost.Parse("{5}{U}{U}"), null, 3, 3, CardType.Creature) { Subtypes = ["Dragon"] },
            ["Dragon's Rage Channeler"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Shaman"] },

            // === UR Delver lands ===
            ["Island"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land) { Subtypes = ["Island"] },
            ["Volcanic Island"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Red), null, null, CardType.Land) { Subtypes = ["Island", "Mountain"] },
            ["Scalding Tarn"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Island", "Mountain"]) },
            ["Mystic Sanctuary"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land),

            // === Shared Premodern cards ===

            // Basic land
            ["Swamp"] = new(null, ManaAbility.Fixed(ManaColor.Black), null, null, CardType.Land) { Subtypes = ["Swamp"] },

            // Dual/Pain lands
            ["Caves of Koilos"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.White, ManaColor.Black], [ManaColor.White, ManaColor.Black]), null, null, CardType.Land),
            ["Llanowar Wastes"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.Black, ManaColor.Green], [ManaColor.Black, ManaColor.Green]), null, null, CardType.Land),
            ["Battlefield Forge"] = new(null, ManaAbility.PainChoice([ManaColor.Colorless, ManaColor.Red, ManaColor.White], [ManaColor.Red, ManaColor.White]), null, null, CardType.Land),
            ["Tainted Field"] = new(null, ManaAbility.Choice(ManaColor.White, ManaColor.Black), null, null, CardType.Land),
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
            ["Darigaaz's Caldera"] = new(null, ManaAbility.Choice(ManaColor.Black, ManaColor.Red, ManaColor.Green), null, null, CardType.Land) { EntersTapped = true },
            ["Treva's Ruins"] = new(null, ManaAbility.Choice(ManaColor.White, ManaColor.Blue, ManaColor.Green), null, null, CardType.Land) { EntersTapped = true },

            // Fetch lands
            ["Flooded Strand"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Plains", "Island"]) },
            ["Bloodstained Mire"] = new(null, null, null, null, CardType.Land) { FetchAbility = new FetchAbility(["Swamp", "Mountain"]) },

            // Mana
            ["Dark Ritual"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                Effect: new AddManaSpellEffect(ManaColor.Black, 3)),
            ["Mox Diamond"] = new(ManaCost.Parse("{0}"), ManaAbility.Choice(ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green), null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new MoxDiamondETBEffect())],
            },

            // Common removal
            ["Disenchant"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Instant,
                TargetFilter.EnchantmentOrArtifact(), new NaturalizeEffect()),
            ["Vindicate"] = new(ManaCost.Parse("{1}{W}{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.AnyPermanent(), new DestroyPermanentEffect()),
            ["Smother"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureWithCMCAtMost(3), new DestroyCreatureEffect()),
            ["Snuff Out"] = new(ManaCost.Parse("{3}{B}"), null, null, null, CardType.Instant,
                TargetFilter.NonBlackCreature(), new DestroyCreatureEffect())
            {
                AlternateCost = new AlternateCost(LifeCost: 4, RequiresControlSubtype: "Swamp"),
            },
            ["Diabolic Edict"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new EdictEffect()),
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
                TargetFilter.Spell(), new ConditionalCounterEffect(3)),
            ["Absorb"] = new(ManaCost.Parse("{W}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new CounterAndGainLifeEffect(3)),

            // Common utility
            ["Fact or Fiction"] = new(ManaCost.Parse("{3}{U}"), null, null, null, CardType.Instant,
                Effect: new FactOrFictionEffect()),
            ["Impulse"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                Effect: new ImpulseEffect()),
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
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{R}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true),
            },
            ["Jackal Pup"] = new(ManaCost.Parse("{R}"), null, 2, 1, CardType.Creature) { Subtypes = ["Hound"] },
            ["Incinerate"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
            ["Shock"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.CreatureOrPlayer(), new DamageEffect(2)),
            ["Sulfuric Vortex"] = new(ManaCost.Parse("{1}{R}{R}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new DamageAllPlayersTriggerEffect(2))],
            },
            ["Cursed Scroll"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true, ManaCost: ManaCost.Parse("{3}")), new DealDamageEffect(2), c => c.IsCreature, CanTargetPlayer: true),
            },
            ["Barbarian Ring"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land)
            {
                ActivatedAbility = new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{R}")),
                    new DealDamageEffect(2),
                    TargetFilter: c => c.IsCreature,
                    CanTargetPlayer: true,
                    Condition: p => p.Graveyard.Count >= 7),
            },

            // === Mono Black Control deck ===
            ["Bane of the Living"] = new(ManaCost.Parse("{2}{B}{B}"), null, 4, 3, CardType.Creature) { Subtypes = ["Zombie"] },
            ["Plague Spitter"] = new(ManaCost.Parse("{2}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Zombie"],
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
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")),
                    new ExileFromOpponentGraveyardEffect()),
            },
            ["Funeral Charm"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                TargetFilter.Player(), new DiscardEffect(1)),
            ["Bottomless Pit"] = new(ManaCost.Parse("{1}{B}{B}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new EachPlayerDiscardsEffect(1))],
            },
            ["The Rack"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new RackDamageEffect())],
            },
            ["Powder Keg"] = new(ManaCost.Parse("{2}"), null, null, null, CardType.Artifact),
            ["Cabal Pit"] = new(null, ManaAbility.Fixed(ManaColor.Black), null, null, CardType.Land)
            {
                ActivatedAbility = new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{B}")),
                    new WeakenTargetEffect(-2, -2),
                    TargetFilter: c => c.IsCreature,
                    Condition: p => p.Graveyard.Count >= 7),
            },
            ["Dust Bowl"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbility = new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true, ManaCost: ManaCost.Parse("{3}")),
                    new DestroyTargetEffect(),
                    TargetFilter: c => c.CardTypes.HasFlag(CardType.Land)
                        && c.Name != "Plains" && c.Name != "Island" && c.Name != "Swamp"
                        && c.Name != "Mountain" && c.Name != "Forest"),
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
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{B}")),
                    new PumpSelfEffect(1, 1)),
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
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{B}")), new BecomeCreatureEffect(1, 1, "Skeleton")),
            },
            ["Skeletal Scrying"] = new(ManaCost.Parse("{1}{B}"), null, null, null, CardType.Instant,
                Effect: new SkeletalScryingEffect()),

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
                        GrantedKeyword: Keyword.FirstStrike,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
                        (card, _) => card.Name == "Knight of Stromgald",
                        GrantedKeyword: Keyword.ProtectionFromWhite,
                        Layer: EffectLayer.Layer6_AbilityAddRemove),
                ],
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{B}")), new PumpSelfEffect(1, 0)),
            },
            ["Phyrexian Rager"] = new(ManaCost.Parse("{2}{B}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Horror"],
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new DrawAndLoseLifeEffect(1, 1))],
            },
            ["Phyrexian Arena"] = new(ManaCost.Parse("{1}{B}{B}"), null, null, null, CardType.Enchantment)
            {
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new DrawAndLoseLifeEffect(1, 1))],
            },

            // === Landstill deck ===
            ["Prohibit"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new ConditionalCounterEffect(2)),
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
            ["Phyrexian Furnace"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact),
            ["Faerie Conclave"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land)
            {
                EntersTapped = true,
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{U}")), new BecomeCreatureEffect(2, 1, "Faerie")),
            },
            ["Mishra's Factory"] = new(null, ManaAbility.Fixed(ManaColor.Colorless), null, null, CardType.Land)
            {
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}")), new BecomeCreatureEffect(2, 2, "Assembly-Worker")),
            },

            // === Oath of Druids deck ===
            ["Terravore"] = new(ManaCost.Parse("{1}{G}{G}"), null, null, null, CardType.Creature)
            {
                Subtypes = ["Lhurgoyf"],
                DynamicBasePower = state => CountLandsInAllGraveyards(state),
                DynamicBaseToughness = state => CountLandsInAllGraveyards(state),
            },
            ["Call of the Herd"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Sorcery,
                Effect: new CreateTokenSpellEffect("Elephant", 3, 3, CardType.Creature, ["Elephant"]))
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
                TargetFilter.EnchantmentOrArtifact(), new NaturalizeEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{G}")),
            },
            ["Reckless Charge"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
                TargetFilter.Creature(), new RecklessChargeEffect())
            {
                FlashbackCost = new FlashbackCost(ManaCost.Parse("{2}{R}")),
            },
            ["Volcanic Spray"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
                Effect: new DamageAllCreaturesEffect(1)),
            ["Quiet Speculation"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Sorcery,
                Effect: new QuietSpeculationEffect()),
            ["Funeral Pyre"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant),
            ["Treetop Village"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land)
            {
                EntersTapped = true,
                ActivatedAbility = new(new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{1}{G}")), new BecomeCreatureEffect(3, 3, "Ape")),
            },

            // === Terrageddon deck ===
            ["Mother of Runes"] = new(ManaCost.Parse("{W}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Human", "Cleric"],
                ActivatedAbility = new(
                    new ActivatedAbilityCost(TapSelf: true),
                    new GrantProtectionEffect(),
                    TargetFilter: c => c.IsCreature),
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
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeCardType: CardType.Land),
                    new Triggers.Effects.GainLifeEffect(2)),
            },

            // === Elves deck ===
            ["Llanowar Elves"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true), new AddManaEffect(ManaColor.Green)),
            },
            ["Fyndhorn Elves"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true), new AddManaEffect(ManaColor.Green)),
            },
            ["Priest of Titania"] = new(ManaCost.Parse("{1}{G}"), null, 1, 1, CardType.Creature)
            {
                Subtypes = ["Elf", "Druid"],
                ActivatedAbility = new(new ActivatedAbilityCost(TapSelf: true),
                    new DynamicAddManaEffect(ManaColor.Green,
                        p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Elf", StringComparer.OrdinalIgnoreCase)))),
            },
            ["Quirion Ranger"] = new(ManaCost.Parse("{G}"), null, 1, 1, CardType.Creature) { Subtypes = ["Elf"] },
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
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new CreateTokensEffect("Squirrel", 1, 1, CardType.Creature, ["Squirrel"], count: 4))],
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
            ["Wall of Roots"] = new(ManaCost.Parse("{1}{G}"), null, 0, 5, CardType.Creature) { Subtypes = ["Plant", "Wall"] },
            ["Ravenous Baloth"] = new(ManaCost.Parse("{2}{G}{G}"), null, 4, 4, CardType.Creature)
            {
                Subtypes = ["Beast"],
                ActivatedAbility = new(new ActivatedAbilityCost(SacrificeSubtype: "Beast"),
                    new Triggers.Effects.GainLifeEffect(4)),
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
                Triggers = [new Trigger(GameEvent.Upkeep, TriggerCondition.Upkeep, new MasticoreUpkeepEffect())],
                ActivatedAbility = new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{2}")),
                    new DealDamageEffect(1),
                    TargetFilter: c => c.IsCreature),
            },
            ["Nantuko Vigilante"] = new(ManaCost.Parse("{3}{G}"), null, 3, 2, CardType.Creature) { Subtypes = ["Insect", "Druid"] },
            ["Yavimaya Granger"] = new(ManaCost.Parse("{2}{G}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Elf"],
                EchoCost = ManaCost.Parse("{2}{G}"),
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SearchLibraryEffect("Forest", optional: true))],
            },
            ["Anger"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
            {
                Subtypes = ["Incarnation"],
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
                ActivatedAbility = new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{G}"), DiscardCardType: CardType.Creature),
                    new SearchLibraryByTypeEffect(CardType.Creature)),
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

            ["Show and Tell"] = new(ManaCost.Parse("{1}{U}{U}"), null, null, null, CardType.Sorcery,
                Effect: new ShowAndTellEffect()),

            ["Sneak Attack"] = new(ManaCost.Parse("{3}{R}"), null, null, null, CardType.Enchantment)
            {
                ActivatedAbility = new(
                    new ActivatedAbilityCost(ManaCost: ManaCost.Parse("{R}")),
                    new SneakAttackPutEffect()),
            },

            ["Emrakul, the Aeons Torn"] = new(ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature)
            {
                IsLegendary = true,
                Subtypes = ["Eldrazi"],
                ShuffleGraveyardOnDeath = true,
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
                ActivatedAbility = new(
                    new ActivatedAbilityCost(PayLife: 7),
                    new DrawCardsActivatedEffect(7)),
            },

            ["Lotus Petal"] = new(ManaCost.Parse("{0}"), null, null, null, CardType.Artifact)
            {
                ActivatedAbility = new(
                    new ActivatedAbilityCost(TapSelf: true, SacrificeSelf: true),
                    new AddAnyManaEffect()),
            },

            ["Spell Pierce"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
                TargetFilter.NoncreatureSpell(), new ConditionalCounterEffect(2)),

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
                TargetFilter.InstantOrSorcerySpell(), new ConditionalCounterEffect(1)),

            ["Pyroblast"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
                TargetFilter.Spell(), new PyroblastEffect()),

            ["Surgical Extraction"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant)
            {
                AlternateCost = new AlternateCost(LifeCost: 2),
            },

            ["Grafdigger's Cage"] = new(ManaCost.Parse("{1}"), null, null, null, CardType.Artifact),

            ["Wipe Away"] = new(ManaCost.Parse("{1}{U}{U}"), null, null, null, CardType.Instant,
                TargetFilter.AnyPermanent(), new BounceTargetEffect()),

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
                EntersTapped = true,
                Triggers = [new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self, new SurveilEffect(1))],
            },

            ["Thoughtseize"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Sorcery,
                TargetFilter.Player(), new ThoughtseizeEffect())
            { Name = "Thoughtseize" },

            ["Fatal Push"] = new(ManaCost.Parse("{B}"), null, null, null, CardType.Instant,
                TargetFilter.Creature(), new FatalPushEffect())
            { Name = "Fatal Push" },

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
