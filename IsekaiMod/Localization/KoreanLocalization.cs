// Display/index guard adapted from the adjacent WOTR-Enhance implementation.
// Only the pack explicitly bound by IsekaiMod is eligible; no TTT catalog is included.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
namespace IsekaiMod.Localization {
  internal static class KoreanLocalization {
    private const string PackTypeName = "TabletopTweaks.Core.Localization.MultiLocalizationPack";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private sealed class Translation {
      internal string English;
      internal string Korean;
      internal bool ProcessTemplates;
    }
    private static readonly Dictionary<string, Translation> ByKey = new Dictionary<string, Translation>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Translation> ByIdentity = new Dictionary<string, Translation>(StringComparer.Ordinal);
    private static readonly HashSet<string> AmbiguousIdentities = new HashSet<string>(StringComparer.Ordinal);
    [ThreadStatic] private static int LookupDepth;
    private static FieldInfo KeyField;
    private static FieldInfo EnglishField;
    private static FieldInfo ProcessField;
    private static FieldInfo ResultTextField;
    private static FieldInfo PackStringsField;
    private static MethodInfo TagMethod;
    private static MethodInfo StringEntryMethod;
    private static int TranslationCount;
    private static bool Registered;
    private static bool Installed;
    private static bool LoggedFailure;
    private static object OwnedPack;
    private static readonly HashSet<object> OwnedStrings = new HashSet<object>();
    internal static void Bind(object pack) {
      OwnedPack = null;
      OwnedStrings.Clear();
      var field = pack == null ? null : pack.GetType().GetField("Strings", Fields);
      var strings = field == null ? null : field.GetValue(pack) as IEnumerable;
      if (strings == null) {
        Main.Log("Isekai Korean localization: incompatible pack Strings API; ownership not bound.");
        return;
      }
      OwnedPack = pack;
      foreach (object value in strings) OwnedStrings.Add(value);
    }
    internal static readonly Dictionary<string, string> UIStrings = new Dictionary<string, string>(StringComparer.Ordinal);
    internal static string Text(string english) {
      string korean;
      return KoreanLocale(LocalizationManager.CurrentLocale) && UIStrings.TryGetValue(english, out korean) ? korean : english;
    }
    internal static string Format(string english, params object[] values) {
      return string.Format(Text(english), values);
    }
    internal static void Register() {
      if (Registered) return;
      KoreanTranslationData.Register();
      Registered = true;
    }
    private static string Identity(string english, bool process) {
      return (process ? "1:" : "0:") + english;
    }
    internal static void Add(string key, string english, string korean, bool process) {
      TranslationCount++;
      Translation value = new Translation { English = english, Korean = korean, ProcessTemplates = process };
      if (!string.IsNullOrEmpty(key)) ByKey.Add(key, value);
      string identity = Identity(english, process);
      Translation existing;
      if (ByIdentity.TryGetValue(identity, out existing) && existing.Korean != korean) {
        AmbiguousIdentities.Add(identity);
      } else {
        ByIdentity[identity] = value;
      }
    }
    internal static void Install(Harmony harmony) {
      if (Installed) return;
      Type packType = AccessTools.TypeByName(PackTypeName);
      Type stringType = AccessTools.TypeByName(PackTypeName + "+MultiLocaleString");
      // Validate the required TTT API before installing display hooks.
      if (packType == null && stringType == null) return;
      MethodInfo stringEntry = stringType == null ? null : AccessTools.Method(stringType, "StringEntry", new Type[] { typeof(Locale) });
      MethodInfo addString = packType == null || stringType == null ? null : AccessTools.Method(packType, "AddString", new Type[] { stringType });
      StringEntryMethod = stringEntry;
      KeyField = stringType == null ? null : stringType.GetField("Key", Fields);
      EnglishField = stringType == null ? null : stringType.GetField("enGB", Fields);
      ProcessField = stringType == null ? null : stringType.GetField("ProcessTemplates", Fields);
      Type entryType = typeof(LocalizationPack).GetNestedType("StringEntry", BindingFlags.Public | BindingFlags.NonPublic);
      ResultTextField = entryType == null ? null : entryType.GetField("Text", Fields);
      PackStringsField = typeof(LocalizationPack).GetField("m_Strings", Fields);
      Type tools = AccessTools.TypeByName("TabletopTweaks.Core.Utilities.DescriptionTools");
      TagMethod = tools == null ? null : AccessTools.Method(tools, "TagEncyclopediaEntries", new Type[] { typeof(string) });
      if (stringEntry == null || addString == null || KeyField == null || EnglishField == null ||
          ProcessField == null || ResultTextField == null || PackStringsField == null || TagMethod == null) {
        Main.Log("Isekai Korean localization: incompatible StringEntry/AddString/DescriptionTools API; no hooks installed.");
        return;
      }
      // Install the lookup guard first. It also neutralizes KoreanTranslationOverride while indexing.
      harmony.Patch(addString, prefix: Hook("AddStringPrefix", Priority.First),
        postfix: Hook("AddStringPostfix", Priority.Last), finalizer: Hook("AddStringFinalizer", Priority.Last));
      harmony.Patch(stringEntry, postfix: Hook("StringEntryPostfix", Priority.Last));
      Installed = true;
      Main.Log("Isekai Korean localization: installed " + TranslationCount + " translations; English lookup guarded, ambiguous fallback identities=" + AmbiguousIdentities.Count + ".");
    }
    private static HarmonyMethod Hook(string method, int priority) {
      HarmonyMethod hook = new HarmonyMethod(typeof(KoreanLocalization), method);
      hook.priority = priority;
      hook.after = new string[] { "KoreanTranslationOverride", "XinkleWotrEnhance" };
      return hook;
    }
    private static void AddStringPrefix(object __instance, object __0, out int __state) {
      __state = LookupDepth;
      if (ReferenceEquals(__instance, OwnedPack)) {
        if (__0 != null) OwnedStrings.Add(__0);
        LookupDepth++;
      }
    }
    private static void AddStringFinalizer(int __state) {
      // Finalizers run on success and failure; preserve exceptions from the original method.
      LookupDepth = __state;
    }
    private static bool KoreanLocale(Locale locale) {
      return locale == Locale.enGB || locale == Locale.esES;
    }
    private static Translation Resolve(object value) {
      string english = EnglishField.GetValue(value) as string;
      if (english == null) return null;
      bool process = (bool)ProcessField.GetValue(value);
      string key = KeyField.GetValue(value) as string;
      Translation result;
      if (key != null && ByKey.TryGetValue(key, out result) &&
          result.English == english && result.ProcessTemplates == process) return result;
      string identity = Identity(english, process);
      if (!AmbiguousIdentities.Contains(identity) && ByIdentity.TryGetValue(identity, out result)) return result;
      return null;
    }
    private static void Translate(object value, object result) {
      Translation translation = Resolve(value);
      if (translation == null) return;
      string text = translation.ProcessTemplates ?
        (string)TagMethod.Invoke(null, new object[] { translation.Korean }) : translation.Korean;
      ResultTextField.SetValue(result, text);

    }
    private static void StringEntryPostfix(object __instance, Locale locale, ref object __result) {
      if (__instance == null || __result == null || !OwnedStrings.Contains(__instance)) return;
      try {
        if (LookupDepth != 0) {
          // Raw English is the index identity, including when another postfix returned Korean.
          ResultTextField.SetValue(__result, EnglishField.GetValue(__instance));
        } else if (KoreanLocale(locale)) {
          Translate(__instance, __result);
        }
      } catch (Exception error) { LogFailure(error); }
    }
    private static void AddStringPostfix(object __instance, object __0) {
      if (__0 == null || !ReferenceEquals(__instance, OwnedPack)) return;
      try {
        // The original AddString may publish a guarded English StringEntry to CurrentPack.
        // Only replace an existing entry; never create keys or edit the source pack.
        LocalizationPack pack = LocalizationManager.CurrentPack;
        string key = KeyField.GetValue(__0) as string;
        IDictionary strings = pack == null ? null : PackStringsField.GetValue(pack) as IDictionary;
        if (strings != null && key != null && strings.Contains(key)) {
          int depth = LookupDepth;
          try {
            LookupDepth = 0;
            // Regenerate display outside the index guard, retaining all locales and other mods' unknown entries.
            strings[key] = StringEntryMethod.Invoke(__0, new object[] { LocalizationManager.CurrentLocale });
          } finally { LookupDepth = depth; }
        }
      } catch (Exception error) { LogFailure(error); }
    }
    private static void LogFailure(Exception error) {
      if (LoggedFailure) return;
      LoggedFailure = true;
      Main.Log("Isekai Korean localization failed; partial display changes may remain: " + error);
    }
  }
}
