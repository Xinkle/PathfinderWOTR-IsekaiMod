using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using HarmonyLib;
using IsekaiMod.Localization;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using TabletopTweaks.Core.Localization;
using Entry = TabletopTweaks.Core.Localization.MultiLocalizationPack.MultiLocaleString;

internal static class Program {
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Legacy(object __instance, Locale locale, ref object __result) {
        var value = (Entry)__instance;
        if (locale == Locale.enGB && value.esES != null) ((LocalizationPack.StringEntry)__result).Text = value.esES;
    }
    private static void EnhanceCollision(object __instance, Locale locale, ref object __result) {
        if (((Entry)__instance).enGB == "Conflict item" && locale == Locale.enGB)
            ((LocalizationPack.StringEntry)__result).Text = "Enhance collision";
    }
    private static Entry Make(string key, string en, bool process = false) {
        return new Entry { Key = key, enGB = en, ProcessTemplates = process };
    }
    public static int Main() {
        try {
            KoreanLocalization.Register(); KoreanLocalization.Register();
            var harmony = new Harmony("IsekaiMod");
            KoreanLocalization.Install(harmony); KoreanLocalization.Install(harmony);
            new Harmony("KoreanTranslationOverride").Patch(AccessTools.Method(typeof(Entry), "StringEntry"),
                postfix: new HarmonyMethod(typeof(Program), "Legacy") { priority = Priority.Last });
            Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Entry), "StringEntry")).Postfixes.Count == 2, "idempotent install");
            var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ko-kr.json")));
            var pack = new MultiLocalizationPack();
            var loaded = Make("loaded", "Isekai Protagonist"); pack.Strings.Add(loaded);
            KoreanLocalization.Bind(pack);
            Check(loaded.StringEntry(Locale.enGB).Text == "이세계 주인공", "loaded JSON ownership");
            foreach (var row in catalog.RootElement.GetProperty("Entries").EnumerateArray()) {
                string key = row.GetProperty("Key").GetString() ?? Guid.NewGuid().ToString("N");
                string en = row.GetProperty("English").GetString();
                string ko = row.GetProperty("Korean").GetString();
                bool process = row.GetProperty("ProcessTemplates").GetBoolean();
                string prefix = process ? "[tag]" : "";
                var entry = Make(key, en, process);
                pack.AddString(entry);
                foreach (var locale in new[] { Locale.enGB, Locale.esES })
                    Check(entry.StringEntry(locale).Text == prefix + ko, "display: " + key);
                Check(entry.StringEntry(Locale.ruRU).Text == prefix + "Russian", "other language");
                Check(entry.enGB == en && entry.esES == null, "source unchanged");
                entry.esES = "legacy conflict";
                Check(entry.StringEntry(Locale.enGB).Text == prefix + ko, "legacy ordering");
                var regenerated = Make(Guid.NewGuid().ToString("N"), en, process);
                pack.AddString(regenerated);
                Check(regenerated.StringEntry(Locale.enGB).Text == prefix + ko, "fallback");
                var changed = Make(key, en + " CHANGED", process);
                pack.AddString(changed);
                Check(changed.StringEntry(Locale.enGB).Text == prefix + changed.enGB, "changed source rejected");
                // Same key/text in a foreign pack must NEVER claim Isekai ownership.
                var foreign = Make(key, en, process);
                Check(foreign.StringEntry(Locale.enGB).Text == prefix + en, "foreign identical source untouched");
            }
            var own = Make("own", "Isekai Protagonist"); pack.AddString(own);
            Check(own.StringEntry(Locale.enGB).Text == "이세계 주인공", "own display");
            var wrongFlag = Make("flag", "Isekai Protagonist", true); pack.AddString(wrongFlag);
            Check(wrongFlag.StringEntry(Locale.enGB).Text == "[tag]Isekai Protagonist", "template mismatch");
            KoreanLocalization.Add("ambiguous1", "Ambiguous", "번역 하나", false);
            KoreanLocalization.Add("ambiguous2", "Ambiguous", "번역 둘", false);
            var ambiguous = Make("other", "Ambiguous"); pack.AddString(ambiguous);
            Check(ambiguous.StringEntry(Locale.enGB).Text == "Ambiguous", "ambiguous fallback");
            var knownAmbiguous = Make("ambiguous1", "Ambiguous"); pack.AddString(knownAmbiguous);
            Check(knownAmbiguous.StringEntry(Locale.enGB).Text == "번역 하나", "known ambiguous key");
            foreach (var locale in new[] { Locale.enGB, Locale.esES, Locale.ruRU }) {
                LocalizationManager.CurrentLocale = locale;
                LocalizationManager.CurrentPack = new LocalizationPack { Locale = locale };
                pack = new MultiLocalizationPack(); KoreanLocalization.Bind(pack);
                var first = Make("first", "Isekai Protagonist"); first.esES = "legacy conflict";
                pack.AddString(first);
                Check(pack.TryGetText(first.enGB, out var found), "English index before apply");
                Check(!pack.Text.ContainsKey("이세계 주인공") && !pack.Text.ContainsKey("legacy conflict"), "no localized indices");
                pack.ApplyToCurrentPack();
                var second = Make("second", first.enGB); second.esES = "legacy conflict"; pack.AddString(second);
                string expected = locale == Locale.ruRU ? "Russian" : "이세계 주인공";
                Check(LocalizationManager.CurrentPack.m_Strings[second.Key].Text == expected, "dynamic publish");
                Check(pack.TryGetText(second.enGB, out found), "English index after apply");
                pack.ResetCache();
                int count = pack.Strings.Count;
                for (int i = 0; i < 5; i++)
                    if (!pack.TryGetText(second.enGB, out found)) pack.AddString(Make(Guid.NewGuid().ToString("N"), second.enGB));
                Check(pack.Strings.Count == count, "no GUID churn after cache reset");
                var unknown = Make("unknown", "Unknown"); unknown.esES = "custom translation"; pack.AddString(unknown);
                Check(LocalizationManager.CurrentPack.m_Strings[unknown.Key].Text == (locale == Locale.ruRU ? "Russian" : "custom translation"), "unknown display retained");
                var foreignPack = new MultiLocalizationPack(); var foreign = Make("foreign", first.enGB);
                foreignPack.AddString(foreign); foreignPack.ApplyToCurrentPack();
                Check(foreign.StringEntry(Locale.enGB).Text == first.enGB, "foreign AddString not owned");
            }
            // A later-installed competing Last-priority owner must still precede Isekai.
            new Harmony("XinkleWotrEnhance").Patch(AccessTools.Method(typeof(Entry), "StringEntry"),
                postfix: new HarmonyMethod(typeof(Program), "EnhanceCollision") { priority = Priority.Last });
            KoreanLocalization.Add("coexist", "Conflict item", "충돌 검사", false);
            LocalizationManager.CurrentLocale = Locale.enGB;
            pack = new MultiLocalizationPack(); KoreanLocalization.Bind(pack);
            var collision = Make("coexist", "Conflict item"); pack.AddString(collision);
            Check(collision.StringEntry(Locale.enGB).Text == "충돌 검사", "Enhance owner ordering");
            Check(pack.TryGetText("Conflict item", out var collisionFound), "coexist English index");
            var foreignCollision = Make("foreign-coexist", "Conflict item");
            Check(foreignCollision.StringEntry(Locale.enGB).Text == "Enhance collision", "foreign owner display retained");
            LocalizationManager.CurrentLocale = Locale.enGB;
            pack = new MultiLocalizationPack(); KoreanLocalization.Bind(pack);
            own = Make("last", "Isekai Protagonist"); pack.AddString(own);
            pack.ThrowDuringAdd = true;
            bool threw = false;
            try { pack.AddString(Make("fail", own.enGB)); } catch (InvalidOperationException e) { threw = e.Message == "original failure"; }
            Check(threw && own.StringEntry(Locale.enGB).Text == "이세계 주인공", "exception preserved/scope restored");
            pack.ThrowDuringAdd = false;
            pack.NestedAdd = () => {
                string display = null;
                var thread = new Thread(() => display = own.StringEntry(Locale.enGB).Text); thread.Start(); thread.Join();
                Check(display == "이세계 주인공", "thread local guard");
                pack.AddString(Make("inner", own.enGB));
            };
            pack.AddString(Make("outer", own.enGB));
            Check(own.StringEntry(Locale.enGB).Text == "이세계 주인공", "nested guard restored");
            Check(pack.Text.Keys.All(k => k != "이세계 주인공"), "nested English index");
            foreach (var pair in catalog.RootElement.GetProperty("UI").EnumerateObject()) {
                Check(KoreanLocalization.Text(pair.Name) == pair.Value.GetString(), "UI lookup");
                if (pair.Name.Contains("{0}")) Check(KoreanLocalization.Format(pair.Name, 123).Contains("123"), "gold format");
            }
            LocalizationManager.CurrentLocale = Locale.deDE;
            Check(KoreanLocalization.Text("Added Content") == "Added Content", "UI other locale");
            Check(KoreanLocalization.Text("Unlisted") == "Unlisted", "UI unknown");
            KoreanLocalization.Bind(new object());
            Check(own.StringEntry(Locale.enGB).Text == own.enGB, "incompatible pack fails closed");
            Console.WriteLine("PASS: all catalog entries, pack isolation, lookup/display guards, locale, legacy, exceptions, nesting, UI");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
