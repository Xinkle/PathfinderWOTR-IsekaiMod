using HarmonyLib;
using IsekaiMod.ModLogic;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using System;
using System.Linq;
using TabletopTweaks.Core.Utilities;
using UnityEngine;
using UnityModManagerNet;
using static Kingmaker.NintendoEventManager;
using Owlcat.Runtime.UI.Tooltips;

namespace IsekaiMod {

    internal static class Main {

        public static ModContextTTTBase IsekaiContext;

        public static bool Load(UnityModManager.ModEntry modEntry) {
            IsekaiContext = new ModContextTTTBase(modEntry);
            try {
                var harmony = new Harmony(modEntry.Info.Id);
                IsekaiContext.ModEntry.OnSaveGUI = OnSaveGUI;
                IsekaiContext.ModEntry.OnGUI = UMMSettingsUI.OnGUI;
                Localization.KoreanLocalization.Register();
                Localization.KoreanLocalization.Bind(IsekaiContext.ModLocalizationPack);
                Localization.KoreanLocalization.Install(harmony);
                harmony.PatchAll();
                PostPatchInitializer.Initialize(IsekaiContext);
                return true;
            }
            catch (Exception e) {
                Log(e.ToString());
                throw e;
            }
        }

        public static void Log(string msg) {
            
            IsekaiContext.Logger.Log(msg);
        }

        public static void LogToGeneral(string message) {
            var thread = LogThreadService.Instance.GetThreadsByChannelType(LogChannelType.Common)
                .OfType<MessageLogThread>()
                .FirstOrDefault();

            if (thread != null) {
                // Create a CombatLogMessage
                var combatLogMessage = new CombatLogMessage(
                    message,                          // The log message text
                    Color.white,                      // Message color (white in this case)
                    PrefixIcon.None,                             // No prefix icon
                    new TooltipTemplateSimple(null, message), // Tooltip with the message text
                    true                              // Enable tooltip
                );

                // Add the message to the thread
                thread.AddMessage(combatLogMessage);
            } else {
                Main.LogDebug("Failed to find a suitable MessageLogThread.");
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogDebug(string msg) {
            IsekaiContext.Logger.Log(msg);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            IsekaiContext.SaveAllSettings();
        }
        // Define a custom Message class if necessary
        public class Message {
            public string Text { get; set; }
            public DateTime Timestamp { get; set; }
            public UnityEngine.Color Color { get; set; }
        }

    }
}