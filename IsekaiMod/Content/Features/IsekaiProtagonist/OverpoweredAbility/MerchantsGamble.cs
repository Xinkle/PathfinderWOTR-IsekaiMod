using System;
using IsekaiMod.Utilities;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items;
using Kingmaker.Localization;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using TabletopTweaks.Core.Utilities;
using UnityEngine;
using static IsekaiMod.Main;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using System.Linq;
using Kingmaker.Designers.Mechanics.Facts;

namespace IsekaiMod.Content.Features.IsekaiProtagonist.OverpoweredAbility {

    internal class MerchantsGamble {
        private const string Name = "Overpowered Ability — Merchant's Gamble";

        // Referenced blueprints
        private static readonly BlueprintItem GoldCoins = BlueprintTools.GetBlueprint<BlueprintItem>("f2bc0997c24e573448c6c91d2be88afa");

        public static void Add() {
            LocalizedString MerchantsGambleDesc = Helpers.CreateString(IsekaiContext, "MerchantsGamble.Description",
                "As a seasoned merchant, you dabble in high-risk ventures. Roll a d20 to determine your return:\n" +
                "1: No profit (0 gold)\n" +
                "2-10: Small profit (500-1,000 gold)\n" +
                "11-19: Moderate profit (1,000-5,000 gold)\n" +
                "20: Jackpot (50,000-100,000 gold).");

            var Icon_Merchants_Gamble = AssetLoader.LoadInternal(IsekaiContext, "Features", "ICON_DUPE_GOLD.png");

            var MerchantsGambleAbility = Helpers.CreateBlueprint<BlueprintAbility>(IsekaiContext, "MerchantsGambleAbility", bp => {
                bp.SetName(IsekaiContext, Name);
                bp.SetDescription(MerchantsGambleDesc);
                bp.AddComponent<AbilityEffectRunAction>(c => {
                    c.Actions = Helpers.CreateActionList(
                        new CustomRollLogic() { GoldCoins = GoldCoins.ToReference<BlueprintItemReference>() }
                    );
                });
                bp.AddComponent<AbilityResourceLogic>(c => {
                    c.m_RequiredResource = CreateResource().ToReference<BlueprintAbilityResourceReference>();
                    c.m_IsSpendResource = true;
                });
                bp.m_Icon = Icon_Merchants_Gamble;
                bp.Type = AbilityType.Special;
                bp.Range = AbilityRange.Personal;
                bp.CanTargetSelf = true;
                bp.Animation = UnitAnimationActionCastSpell.CastAnimationStyle.Self;
                bp.ActionType = UnitCommand.CommandType.Standard;
                bp.LocalizedDuration = StaticReferences.Strings.Null;
                bp.LocalizedSavingThrow = StaticReferences.Strings.Null;
            });

            var MerchantsGambleFeature = Helpers.CreateBlueprint<BlueprintFeature>(IsekaiContext, "MerchantsGambleFeature", bp => {
                bp.SetName(IsekaiContext, Name);
                bp.SetDescription(MerchantsGambleDesc);
                bp.m_Icon = Icon_Merchants_Gamble;
                bp.AddComponent<AddFacts>(c => {
                    c.m_Facts = new BlueprintUnitFactReference[] { MerchantsGambleAbility.ToReference<BlueprintUnitFactReference>() };
                });
                bp.AddComponent<AddAbilityResources>(c => {
                    c.m_Resource = CreateResource().ToReference<BlueprintAbilityResourceReference>();
                    c.RestoreAmount = true;
                });
            });

            OverpoweredAbilitySelection.AddToSelection(MerchantsGambleFeature);
        }

        private static BlueprintAbilityResource CreateResource() {
            return Helpers.CreateBlueprint<BlueprintAbilityResource>(IsekaiContext, "MerchantsGambleResource", bp => {
                bp.m_MaxAmount = new BlueprintAbilityResource.Amount() {
                    BaseValue = 1,
                    IncreasedByLevel = true,
                    LevelIncrease = 1,
                    IncreasedByStat = true,
                    ResourceBonusStat = Kingmaker.EntitySystem.Stats.StatType.Charisma
                };
                bp.m_UseMax = true;
            });
        }
    }

    public class CustomRollLogic : ContextAction {
        public BlueprintItemReference GoldCoins { get; set; }

        public override string GetCaption() {
            return "Simulates a d20 roll for Merchant's Gamble.";
        }

        public override void RunAction() {
            int roll = UnityEngine.Random.Range(1, 21);
            int goldAmount = 0;

            // Generate a random story-based message
            string message = GetRandomMessage(roll, ref goldAmount);

            // Add gold to player's inventory
            if (goldAmount > 0) {
                Game.Instance.Player.Inventory.Add(GoldCoins, goldAmount);
            }

            // Log the message to the Combat Log
            LogToCombatLog(message);
        }

        private void LogToCombatLog(string message) {
            var combatLogThread = LogThreadService.Instance.GetThreadsByChannelType(LogChannelType.Common)
                .OfType<MessageLogThread>()
                .FirstOrDefault();

            if (combatLogThread != null) {
                var combatLogMessage = new CombatLogMessage(
                    message,
                    new Color(0.6f, 0.4f, 0.9f), // Purple color
                    PrefixIcon.None,
                    new TooltipTemplateSimple(null, message),
                    true
                );
                combatLogThread.AddMessage(combatLogMessage);
            } else {
                Main.LogDebug("Failed to find a suitable MessageLogThread.");
            }
        }

        private string GetRandomMessage(int roll, ref int goldAmount) {
            System.Random random = new System.Random();

            if (roll == 1) {
                string[] failureMessages = {
            IsekaiMod.Localization.KoreanLocalization.Text("Your gamble failed! No gold gained."),
            IsekaiMod.Localization.KoreanLocalization.Text("Your negotiation with a merchant guild fell apart when their leader accused you of favoritism. No gold gained."),
            IsekaiMod.Localization.KoreanLocalization.Text("A caravan of rare goods you invested in was destroyed by demon raiders. Your gamble failed."),
            IsekaiMod.Localization.KoreanLocalization.Text("Your attempt to fund a town's defenses in exchange for future profits backfired when the town rebelled. No gold gained."),
            IsekaiMod.Localization.KoreanLocalization.Text("The shipment of rare spices you financed was confiscated by corrupt officials. No profits this time."),
            IsekaiMod.Localization.KoreanLocalization.Text("An enchanted forge you invested in turned out to be cursed, leaving you with no returns."),
            IsekaiMod.Localization.KoreanLocalization.Text("Your trusted advisor made a disastrous trade deal on your behalf, losing all your capital. No gold gained."),
            IsekaiMod.Localization.KoreanLocalization.Text("An alchemical experiment you backed exploded spectacularly, leaving you with nothing but soot-stained robes."),
            IsekaiMod.Localization.KoreanLocalization.Text("The merchant caravan you tried to tax disappeared overnight, taking your investment with them. No gold gained."),
            IsekaiMod.Localization.KoreanLocalization.Text("The demons launched a surprise attack, destroying the grain reserves you planned to sell. Your gamble failed."),
            IsekaiMod.Localization.KoreanLocalization.Text("A rival faction sabotaged your plans to monopolize the holy relic market. No gold gained.")
                };
                return failureMessages[random.Next(failureMessages.Length)];
            } else if (roll >= 2 && roll <= 10) {
                goldAmount = UnityEngine.Random.Range(500, 1001);
                string[] smallProfitMessages = {
                    IsekaiMod.Localization.KoreanLocalization.Format("Your negotiation earned {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your investment in enchanted boots for scouts paid off slightly, earning {0} gold from increased efficiency.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A small-town blacksmith repaid your earlier support with a modest donation of {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You taxed passing caravans at the borderlands, collecting {0} gold in tolls.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your negotiation for better supply rates with a nearby town earned you {0} gold in savings.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You leased unused Crusader wagons to local traders, earning {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("An investment in portable holy water vials sold during a festival netted you {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your venture into selling Crusader-themed banners and souvenirs earned {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You rented out a portion of your army’s barracks to traveling merchants, earning {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A small alchemical workshop you funded paid back {0} gold in profits from potion sales.", goldAmount)
                };
                return smallProfitMessages[random.Next(smallProfitMessages.Length)];
            } else if (roll >= 11 && roll <= 19) {
                goldAmount = UnityEngine.Random.Range(1000, 5001);
                string[] moderateProfitMessages = {
                    IsekaiMod.Localization.KoreanLocalization.Format("You earned {0} gold in profits.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You struck a deal with a merchant guild to supply armor, earning {0} gold in commission.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your investment in a town's brewery paid off handsomely, netting {0} gold in profits.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A clever trade route you devised bypassed demon patrols, bringing {0} gold into your coffers.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You negotiated a lucrative contract with alchemists for mass-producing healing potions, earning {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A wealthy noble funded your Crusade efforts after you secured their town’s safety, donating {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You implemented a new taxation system that brought {0} gold into the Crusade’s treasury.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("An exclusive deal with a weaponsmith for enchanted weapons brought you {0} gold in returns.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A partnership with a city council to rebuild their defenses earned you {0} gold in shared profits.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your foresight to invest in winter supplies early earned {0} gold in savings and profits.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A booming trade in enchanted Crusader cloaks brought {0} gold into the treasury.", goldAmount)
                };
                return moderateProfitMessages[random.Next(moderateProfitMessages.Length)];
            } else if (roll == 20) {
                goldAmount = UnityEngine.Random.Range(50000, 100001);
                string[] jackpotMessages = {
                    IsekaiMod.Localization.KoreanLocalization.Format("Jackpot! You earned {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You orchestrated a grand auction of rare relics, earning a staggering {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your deal with a merchant prince secured a massive donation of {0} gold for the Crusade.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A large-scale operation to reclaim a demon-infested mine yielded {0} gold in rare minerals.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You cornered the market on blessed weapons, earning an incredible {0} gold in profits.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your political alliance with a wealthy kingdom resulted in a massive gift of {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You persuaded an alchemical consortium to donate {0} gold after saving their laboratories from demons.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your investment in a major city’s reconstruction efforts paid off spectacularly, earning {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("You sold the naming rights of a newly established fort, earning a colossal {0} gold.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("Your Crusade-themed festival drew visitors from across the realm, bringing in {0} gold in revenues.", goldAmount),
                    IsekaiMod.Localization.KoreanLocalization.Format("A wealthy benefactor, inspired by your leadership, donated {0} gold to the Crusade.", goldAmount)
                };
                return jackpotMessages[random.Next(jackpotMessages.Length)];
            } else {
                return IsekaiMod.Localization.KoreanLocalization.Text("Unexpected roll result.");
            }
        }
    }
}

