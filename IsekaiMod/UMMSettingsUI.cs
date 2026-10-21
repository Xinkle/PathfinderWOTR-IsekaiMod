using IsekaiMod.Config;
using IsekaiMod.Localization;
using IsekaiMod.Utilities;
using System.Linq;
using TabletopTweaks.Core.UMMTools;
using UnityModManagerNet;
using static IsekaiMod.Main;

namespace IsekaiMod {

    public static class UMMSettingsUI {
        private static int selectedTab;

        public static void OnGUI(UnityModManager.ModEntry modEntry) {
            UI.AutoWidth();
            UI.TabBar(ref selectedTab,
                    () => UI.Label(KoreanLocalization.Text("Select your preferred settings and restart your game.").yellow().bold()),
                   new NamedAction(KoreanLocalization.Text("Added Content"), () => SettingsTabs.AddedContent()),
                   new NamedAction(KoreanLocalization.Text("Appearance"), () => SettingsTabs.Appearance()),
                   new NamedAction(KoreanLocalization.Text("Settings"), () => SettingsTabs.Settings())
            );
        }
    }

    internal static class SettingsTabs {
        private static readonly AddedContent addedContent = IsekaiContext.AddedContent;
        public static void AddedContent() {
            UI.Div();
            UI.Toggle(KoreanLocalization.Text("New Settings Off By Default").bold(), ref addedContent.NewSettingsOffByDefault);
            using (UI.VerticalScope()) {
                KoreanSettingGroup("Isekai", addedContent.Isekai);
                KoreanSettingGroup("Other", addedContent.Other);
            }
        }
        private static void KoreanSettingGroup(string title, TabletopTweaks.Core.Config.SettingGroup group) {
            // Do not translate dictionary keys, SettingData.Description, or serialized settings.
            if (group.Settings.Count == 0) return;
            using (UI.HorizontalScope()) {
                SetttingUI.Toggle("", !group.DisableAll, enabled => group.DisableAll = !enabled);
                UI.DisclosureToggle(KoreanLocalization.Text(title).bold(), ref group.IsExpanded, 180f);
            }
            if (!group.IsExpanded) return;
            foreach (var entry in group.Settings) {
                using (UI.HorizontalScope()) {
                    UI.Space(30);
                    SetttingUI.Toggle(KoreanLocalization.Text(entry.Key), group.IsEnabled(entry.Key),
                        enabled => group.ChangeSetting(entry.Key, enabled), UI.Width(300));
                    UI.Label(entry.Value.Homebrew ? KoreanLocalization.Text("Homebrew").yellow() : "", UI.Width(80));
                    UI.Label(KoreanLocalization.Text(entry.Value.Description).green());
                }
                UI.Div();
            }
        }
        public static void Appearance() {
            UI.Div();
            UI.Label(KoreanLocalization.Text("Set the Isekai Protagonist's default clothes.") + " " + KoreanLocalization.Text("Remember to restart!").orange());
            using (UI.HorizontalScope()) {
                var selected = addedContent.IsekaiDefaultClothes;
                var classNames = StaticReferences.BaseClasses.Select(c => c.LocalizedName.ToString()).ToArray();
                var titles = classNames.Select((a, i) => i == selected ? a.orange().bold() : a).ToArray();
                addedContent.IsekaiDefaultClothes = UnityEngine.GUILayout.SelectionGrid(selected, titles, 5, UI.Width(600));
            }
        }
        public static void Settings() {
            UI.Div();
            UI.Toggle(KoreanLocalization.Text("Exclude Companions and Mercenaries from having the Isekai Protagonist Class."), ref addedContent.ExcludeCompanionsFromIsekaiClass);
            UI.Toggle(KoreanLocalization.Text("Restrict Exceptional Feats to the Isekai Protagonist Class."), ref addedContent.RestrictExceptionalFeats);
            UI.Toggle(KoreanLocalization.Text("Restrict Mythic Overpowered Abilities to the Isekai Protagonist Class."), ref addedContent.RestrictMythicOPAbility);
            UI.Toggle(KoreanLocalization.Text("Restrict Mythic Special Powers to the Isekai Protagonist Class."), ref addedContent.RestrictMythicSpecialPower);
            UI.Toggle(KoreanLocalization.Text("Mythic Overpowered Abilities no longer restricted to one."), ref addedContent.MultipleMythicOPAbility);
            UI.Toggle(KoreanLocalization.Text("Mythic Special Powers no longer restricted to one."), ref addedContent.MultipleMythicSpecialPower);
            UI.Toggle(KoreanLocalization.Text("Apply a merge function on all canon baseclass spell lists to create the Isekai Spell list."), ref addedContent.MergeIsekaiSpellList);
            UI.Slider(KoreanLocalization.Text("Isekai Protagonist Spells Known Increment"), ref addedContent.IsekaiSpellsKnownIncrement, 1, 6, 6, KoreanLocalization.Text("spells per level"), UnityEngine.GUILayout.ExpandWidth(false));
            UI.Div(0, 25);
            UI.HStack(KoreanLocalization.Text("Disable Spellbook"), 1,
                () => { UI.Toggle(KoreanLocalization.Text("Disable Edge Lord Spellbook."), ref addedContent.DisableSpellbookEdgeLord); },
                () => { UI.Toggle(KoreanLocalization.Text("Disable God Emperor Spellbook."), ref addedContent.DisableSpellbookGodEmperor); },
                () => { UI.Toggle(KoreanLocalization.Text("Disable Hero Spellbook."), ref addedContent.DisableSpellbookHero); },
                () => { UI.Toggle(KoreanLocalization.Text("Disable Mastermind Spellbook."), ref addedContent.DisableSpellbookMastermind); },
                () => { UI.Toggle(KoreanLocalization.Text("Disable Overlord Spellbook."), ref addedContent.DisableSpellbookOverlord); }
                );
        }
    }
}