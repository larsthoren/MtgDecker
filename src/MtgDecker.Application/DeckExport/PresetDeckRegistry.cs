using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.DeckExport;

public record PresetDeck(string Name, Format Format, string DeckTextMtgo);

public static class PresetDeckRegistry
{
    public static IReadOnlyList<PresetDeck> All { get; } = new List<PresetDeck>
    {
        new("PM Goblins", Format.Premodern, """
            4 Goblin Lackey
            4 Goblin Matron
            4 Goblin Piledriver
            4 Goblin Ringleader
            4 Goblin Warchief
            4 Mogg Fanatic
            3 Gempalm Incinerator
            3 Siege-Gang Commander
            1 Goblin King
            1 Goblin Pyromancer
            1 Goblin Sharpshooter
            1 Goblin Tinkerer
            1 Skirk Prospector
            2 Naturalize
            8 Mountain
            4 Karplusan Forest
            4 Rishadan Port
            4 Wooded Foothills
            2 Wasteland
            1 Forest
            SB: 4 Pyroblast
            SB: 2 Naturalize
            SB: 2 Pyrokinesis
            SB: 2 Tormod's Crypt
            SB: 2 Tranquil Domain
            SB: 1 Anarchy
            SB: 1 Goblin Tinkerer
            SB: 1 Sulfuric Vortex
            """),

        new("PM Enchantress", Format.Premodern, """
            4 Argothian Enchantress
            3 Swords to Plowshares
            2 Replenish
            4 Enchantress's Presence
            4 Wild Growth
            3 Exploration
            3 Mirri's Guile
            3 Opalescence
            3 Parallax Wave
            3 Sterling Grove
            2 Aura of Silence
            2 Seal of Cleansing
            1 Solitary Confinement
            1 Sylvan Library
            7 Forest
            4 Brushland
            4 Windswept Heath
            3 Plains
            3 Serra's Sanctum
            1 Wooded Foothills
            SB: 2 Carpet of Flowers
            SB: 2 Circle of Protection: Red
            SB: 2 Gaea's Blessing
            SB: 2 Tormod's Crypt
            SB: 2 Xantid Swarm
            SB: 1 Seal of Cleansing
            SB: 1 Solitary Confinement
            SB: 1 Swords to Plowshares
            SB: 1 Tsabo's Web
            SB: 1 Worship
            """),

        new("PM Sligh (RDW)", Format.Premodern, """
            4 Ball Lightning
            4 Grim Lavamancer
            4 Jackal Pup
            4 Mogg Fanatic
            4 Fireblast
            2 Flame Rift
            4 Incinerate
            4 Lightning Bolt
            3 Shock
            3 Cursed Scroll
            4 Sulfuric Vortex
            2 Barbarian Ring
            2 Mishra's Factory
            12 Mountain
            4 Wooded Foothills
            SB: 2 Anarchy
            SB: 2 Overload
            SB: 2 Price of Progress
            SB: 2 Pyroblast
            SB: 2 Pyroclasm
            SB: 3 Red Elemental Blast
            SB: 2 Tormod's Crypt
            """),

        new("PM Mono Black Aggro", Format.Premodern, """
            1 Graveborn Muse
            4 Hypnotic Specter
            3 Nantuko Shade
            4 Ravenous Rats
            4 Withered Wretch
            4 Cabal Therapy
            4 Dark Ritual
            1 Diabolic Edict
            4 Duress
            1 Funeral Charm
            1 Skeletal Scrying
            3 Smother
            1 Snuff Out
            2 Cursed Scroll
            4 Mishra's Factory
            1 Spawning Pool
            16 Swamp
            2 Wasteland
            SB: 1 Diabolic Edict
            SB: 2 Dystopia
            SB: 3 Engineered Plague
            SB: 2 Gloom
            SB: 1 Phyrexian Furnace
            SB: 2 Plague Spitter
            SB: 1 Spinning Darkness
            SB: 1 Tormod's Crypt
            SB: 2 Zombie Infestation
            """),

        new("PM Elves", Format.Premodern, """
            1 Anger
            1 Caller of the Claw
            2 Deranged Hermit
            4 Fyndhorn Elves
            4 Llanowar Elves
            2 Masticore
            4 Multani's Acolyte
            2 Nantuko Vigilante
            4 Priest of Titania
            3 Quirion Ranger
            1 Ravenous Baloth
            1 Squee, Goblin Nabob
            1 Wall of Blossoms
            1 Wall of Roots
            4 Wirewood Symbiote
            1 Yavimaya Granger
            4 Survival of the Fittest
            11 Forest
            4 Gaea's Cradle
            1 Mountain
            4 Wooded Foothills
            SB: 2 Call of the Herd
            SB: 1 Caller of the Claw
            SB: 1 Crumble
            SB: 1 Nantuko Vigilante
            SB: 4 Naturalize
            SB: 1 Ravenous Baloth
            SB: 1 Tormod's Crypt
            SB: 2 Tranquil Domain
            SB: 2 Wall of Blossoms
            """),

        new("PM Terrageddon", Format.Premodern, """
            1 Mother of Runes
            4 Nimble Mongoose
            4 Terravore
            3 Armageddon
            3 Call of the Herd
            1 Cataclysm
            1 Disenchant
            1 Naturalize
            4 Swords to Plowshares
            4 Vindicate
            4 Mox Diamond
            3 Sylvan Library
            1 Zuran Orb
            1 Battlefield Forge
            1 Caves of Koilos
            2 Darigaaz's Caldera
            2 Forest
            4 Gemstone Mine
            1 Llanowar Wastes
            2 Plains
            2 Rishadan Port
            3 Treetop Village
            4 Wasteland
            4 Windswept Heath
            SB: 2 Aura of Silence
            SB: 2 Circle of Protection: Red
            SB: 1 Cursed Totem
            SB: 2 Pyroclasm
            SB: 3 Red Elemental Blast
            SB: 2 Simoon
            SB: 1 Sphere of Resistance
            SB: 1 Tranquil Domain
            SB: 1 Zuran Orb
            """),

        new("PM Deadguy Ale", Format.Premodern, """
            3 Exalted Angel
            4 Hypnotic Specter
            2 Knight of Stromgald
            4 Phyrexian Rager
            2 Withered Wretch
            4 Dark Ritual
            4 Duress
            4 Gerrard's Verdict
            4 Swords to Plowshares
            4 Vindicate
            2 Phyrexian Arena
            1 Phyrexian Furnace
            4 Caves of Koilos
            3 Plains
            10 Swamp
            2 Tainted Field
            3 Wasteland
            SB: 1 Diabolic Edict
            SB: 3 Disenchant
            SB: 3 Engineered Plague
            SB: 2 Perish
            SB: 2 Presence of the Master
            SB: 1 Radiant's Dragoons
            SB: 3 Warmth
            """),

        new("PM Landstill", Format.Premodern, """
            1 Exalted Angel
            1 Absorb
            4 Counterspell
            2 Decree of Justice
            2 Disenchant
            3 Fact or Fiction
            4 Impulse
            2 Mana Leak
            1 Prohibit
            4 Swords to Plowshares
            2 Wrath of God
            1 Humility
            2 Phyrexian Furnace
            1 Powder Keg
            3 Standstill
            4 Adarkar Wastes
            2 Coastal Tower
            3 Dust Bowl
            1 Faerie Conclave
            4 Flooded Strand
            5 Island
            4 Mishra's Factory
            3 Plains
            1 Skycloud Expanse
            SB: 2 Annul
            SB: 2 Blue Elemental Blast
            SB: 1 Circle of Protection: Red
            SB: 1 Erase
            SB: 1 Exalted Angel
            SB: 2 Hydroblast
            SB: 3 Meddling Mage
            SB: 1 Phyrexian Furnace
            SB: 2 Teferi's Response
            """),

        new("PM Oath of Druids", Format.Premodern, """
            3 Terravore
            3 Call of the Herd
            3 Cataclysm
            1 Deep Analysis
            2 Funeral Pyre
            1 Naturalize
            2 Quiet Speculation
            1 Ray of Revelation
            1 Reckless Charge
            4 Swords to Plowshares
            1 Volcanic Spray
            4 Mox Diamond
            4 Oath of Druids
            3 Sylvan Library
            2 Adarkar Wastes
            1 City of Brass
            2 Forest
            3 Gemstone Mine
            2 Plains
            3 Rishadan Port
            4 Treetop Village
            2 Treva's Ruins
            4 Wasteland
            4 Windswept Heath
            SB: 3 Annul
            SB: 1 Aura of Silence
            SB: 1 Krosan Reclamation
            SB: 4 Meddling Mage
            SB: 1 Powder Keg
            SB: 1 Ray of Revelation
            SB: 1 Terravore
            SB: 2 Thornscape Apprentice
            SB: 1 Volcanic Spray
            """),

        new("PM Mono Black Control", Format.Premodern, """
            2 Bane of the Living
            3 Plague Spitter
            3 Withered Wretch
            3 Cabal Therapy
            4 Dark Ritual
            2 Diabolic Edict
            4 Duress
            3 Funeral Charm
            1 Smother
            1 Snuff Out
            3 Bottomless Pit
            2 Cursed Scroll
            3 Powder Keg
            4 The Rack
            1 Cabal Pit
            1 Dust Bowl
            4 Mishra's Factory
            13 Swamp
            3 Wasteland
            SB: 3 Dystopia
            SB: 2 Engineered Plague
            SB: 2 Ensnaring Bridge
            SB: 2 Gloom
            SB: 2 Phyrexian Arena
            SB: 4 Rejuvenation Chamber
            """),

        new("Legacy Dimir Tempo", Format.Legacy, """
            4 Tamiyo, Inquisitive Student
            4 Orcish Bowmasters
            3 Murktide Regent
            2 Kaito, Bane of Nightmares
            4 Brainstorm
            4 Ponder
            4 Thoughtseize
            4 Force of Will
            4 Fatal Push
            3 Daze
            2 Brazen Borrower
            1 Snuff Out
            4 Polluted Delta
            4 Underground Sea
            4 Wasteland
            2 Flooded Strand
            1 Scalding Tarn
            1 Misty Rainforest
            1 Bloodstained Mire
            1 Undercity Sewers
            1 Island
            1 Swamp
            """),

        new("Legacy Sneak and Show", Format.Legacy, """
            4 Show and Tell
            4 Sneak Attack
            3 Emrakul, the Aeons Torn
            3 Griselbrand
            4 Brainstorm
            4 Ponder
            4 Force of Will
            2 Spell Pierce
            3 Lotus Petal
            4 Ancient Tomb
            3 City of Traitors
            2 Intuition
            4 Island
            3 Scalding Tarn
            2 Misty Rainforest
            1 Volcanic Island
            SB: 3 Blood Moon
            SB: 2 Pyroclasm
            SB: 2 Flusterstorm
            SB: 2 Pyroblast
            SB: 2 Surgical Extraction
            SB: 2 Grafdigger's Cage
            SB: 2 Wipe Away
            """),
    }.AsReadOnly();
}
