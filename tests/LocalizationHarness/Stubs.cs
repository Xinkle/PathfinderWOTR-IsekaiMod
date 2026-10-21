using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace IsekaiMod {
  internal static class Main {
    internal static readonly ModLogger Logger = new ModLogger();
    internal static void Log(string message) { Logger.Log(message); }
    internal sealed class ModLogger {
      internal List<string> Errors = new List<string>();
      internal void Log(string message) { Console.WriteLine(message); }
      internal void Error(string message) { Errors.Add(message); Console.WriteLine(message); }
    }
  }
}
namespace Kingmaker.Localization.Shared { public enum Locale { enGB=0, ruRU=1, deDE=2, frFR=3, zhCN=4, esES=6 } }
namespace Kingmaker.Localization {
  using Kingmaker.Localization.Shared;
  public class LocalizationPack {
    internal class StringEntry { public string Text; }
    internal Dictionary<string, StringEntry> m_Strings = new Dictionary<string, StringEntry>();
    public Locale Locale;
  }
  public static class LocalizationManager {
    public static Locale CurrentLocale = Locale.enGB;
    public static LocalizationPack CurrentPack = new LocalizationPack();
  }
}
namespace TabletopTweaks.Core.Utilities {
  public static class DescriptionTools {
    public static string TagEncyclopediaEntries(string text) { return "[tag]" + text; }
  }
}
namespace TabletopTweaks.Core.Localization {
  using Kingmaker.Localization;
  using Kingmaker.Localization.Shared;
  using TabletopTweaks.Core.Utilities;
  public class MultiLocalizationPack {
    public class MultiLocaleString {
      public string Key;
      public string SimpleName;
      public bool ProcessTemplates;
      public string enGB;
      public string esES;
      public string ruRU = "Russian";
      [MethodImpl(MethodImplOptions.NoInlining)]
      internal LocalizationPack.StringEntry StringEntry(Locale locale) {
        string value = locale == Locale.esES ? esES : locale == Locale.ruRU ? ruRU : enGB;
        if (String.IsNullOrEmpty(value)) value = enGB;
        return new LocalizationPack.StringEntry { Text = ProcessTemplates ? DescriptionTools.TagEncyclopediaEntries(value) : value };
      }
    }
    public readonly List<MultiLocaleString> Strings = new List<MultiLocaleString>();
    private SortedDictionary<string, MultiLocaleString> text;
    private bool applied;
    public bool ThrowDuringAdd;
    public Action NestedAdd;
    public SortedDictionary<string, MultiLocaleString> Text {
      get {
        if (text == null) {
          text = new SortedDictionary<string, MultiLocaleString>(StringComparer.InvariantCulture);
          foreach (MultiLocaleString value in Strings) text[value.enGB] = value;
        }
        return text;
      }
    }
    public bool TryGetText(string english, out MultiLocaleString value) { return Text.TryGetValue(english, out value); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddString(MultiLocaleString newString) {
      if (NestedAdd != null) { Action nested = NestedAdd; NestedAdd = null; nested(); }
      if (ThrowDuringAdd) { newString.StringEntry(Locale.enGB); throw new InvalidOperationException("original failure"); }
      Text[newString.StringEntry(applied ? LocalizationManager.CurrentLocale : Locale.enGB).Text] = newString;
      Strings.Add(newString);
      if (applied) LocalizationManager.CurrentPack.m_Strings[newString.Key] = newString.StringEntry(LocalizationManager.CurrentLocale);
    }
    public void ApplyToCurrentPack() {
      foreach (MultiLocaleString value in Strings) LocalizationManager.CurrentPack.m_Strings[value.Key] = value.StringEntry(LocalizationManager.CurrentLocale);
      applied = true;
    }
    public void ResetCache() { text = null; }
  }
}
