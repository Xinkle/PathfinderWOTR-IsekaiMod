// Compile-only host stand-in; no game code executes and this assembly is never shipped.
namespace IsekaiMod {
    internal static class Main {
        internal static ModLogic.ModContextTTTBase IsekaiContext;
        internal static void Log(string message) { }
    }
}
