using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Sinka
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // No BepInProcess, but this is a client-side mod in practice: snapping is placement-time,
    // and Sinka is deliberately out of the server package and the server build list. It
    // also no longer registers with Core's version gate, so nothing here has to agree with
    // anything running anywhere else - which is the point, and why it can be played and
    // versioned on its own.
    public class SinkaPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.sinka";
        public const string PluginName = "Sinka";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Robbin Thijssen";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            SinkaConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ScenePatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            if (ZNetScene.instance == null) return;
            SnapPoints.Apply();
        }
    }

    internal static class ScenePatches
    {
        /// <summary>
        /// Snap points are added to the prefabs, so this has to land before anything is
        /// built from them. Every chest in the world - including ones loaded back out of a
        /// save - is instantiated from these prefabs after Awake, so they all inherit the
        /// points rather than only newly placed ones.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static void AddSnapPointsOnScene()
        {
            SnapPoints.Apply();
        }
    }
}
