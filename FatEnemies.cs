using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace FatEnemies
{
    [BepInPlugin(Guid, "Fat Enemies", "1.0.0")]
    public class FatEnemiesPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.example.fatenemies";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> Width;
        internal static ConfigEntry<float> Height;
        internal static ConfigEntry<float> SpeedMultiplier;
        internal static ConfigEntry<float> AnimationSpeedMultiplier;
        internal static ConfigEntry<bool> AffectTamed;
        internal static ConfigEntry<bool> AffectBosses;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Turn the fatness on or off.");
            Width = Config.Bind("General", "Width", 2.5f,
                new ConfigDescription("Sideways/forward-back multiplier. 1 = normal, 2.5 = very fat.",
                    new AcceptableValueRange<float>(0.5f, 6f)));
            Height = Config.Bind("General", "Height", 1.0f,
                new ConfigDescription("Vertical multiplier. Keep near 1 for a squat, round look.",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            SpeedMultiplier = Config.Bind("General", "SpeedMultiplier", 0.6f,
                new ConfigDescription("Movement speed multiplier (walk/run/swim/fly). 1 = vanilla, 0.6 = fat and slow.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            AnimationSpeedMultiplier = Config.Bind("General", "AnimationSpeedMultiplier", 0.6f,
                new ConfigDescription("Animation playback speed multiplier. Also slows attack wind-ups. 1 = vanilla.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            AffectTamed = Config.Bind("General", "AffectTamed", false, "Also fatten tamed creatures.");
            AffectBosses = Config.Bind("General", "AffectBosses", true, "Also fatten bosses.");

            new Harmony(Guid).PatchAll();
        }
    }

    // Remembers what we applied so re-applying (e.g. after a level-up rescale) never compounds.
    internal class FatMarker : MonoBehaviour
    {
        public Vector3 Applied = Vector3.one;
        public float SpeedApplied = 1f;

        private Animator _animator;
        private float _lastSet = -1f;

        private void Awake() => _animator = GetComponentInChildren<Animator>();

        // The game can reassign animator.speed at any time (freezes, sync, etc.), so each frame we
        // treat any value we didn't set as the new baseline and scale that.
        private void LateUpdate()
        {
            if (_animator == null) return;
            float factor = FatEnemiesPlugin.Enabled.Value ? FatEnemiesPlugin.AnimationSpeedMultiplier.Value : 1f;
            float current = _animator.speed;
            float baseline = Mathf.Approximately(current, _lastSet) ? current / Mathf.Max(_lastFactor, 0.0001f) : current;
            _lastFactor = factor;
            _lastSet = baseline * factor;
            _animator.speed = _lastSet;
        }

        private float _lastFactor = 1f;
    }

    internal static class Fatten
    {
        public static void Apply(Character c)
        {
            if (c == null || c.IsPlayer() || !FatEnemiesPlugin.Enabled.Value) return;
            if (!FatEnemiesPlugin.AffectTamed.Value && c.IsTamed()) return;
            if (!FatEnemiesPlugin.AffectBosses.Value && c.IsBoss()) return;

            var marker = c.GetComponent<FatMarker>() ?? c.gameObject.AddComponent<FatMarker>();
            var t = c.transform;

            // Undo previous fattening, then apply the current setting.
            var s = t.localScale;
            var prev = marker.Applied;
            s = new Vector3(s.x / prev.x, s.y / prev.y, s.z / prev.z);

            var fat = new Vector3(FatEnemiesPlugin.Width.Value, FatEnemiesPlugin.Height.Value, FatEnemiesPlugin.Width.Value);
            t.localScale = Vector3.Scale(s, fat);
            marker.Applied = fat;

            // Fatter means slower. Undo the previous factor first so repeated calls don't compound.
            float speed = FatEnemiesPlugin.SpeedMultiplier.Value;
            float ratio = speed / marker.SpeedApplied;
            c.m_walkSpeed *= ratio;
            c.m_runSpeed *= ratio;
            c.m_swimSpeed *= ratio;
            c.m_flySlowSpeed *= ratio;
            c.m_flyFastSpeed *= ratio;
            marker.SpeedApplied = speed;
        }
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    internal static class CharacterAwakePatch
    {
        private static void Postfix(Character __instance) => Fatten.Apply(__instance);
    }

    // Valheim rescales starred/leveled creatures here, so fatten again afterward.
    [HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
    internal static class CharacterSetLevelPatch
    {
        private static void Postfix(Character __instance) => Fatten.Apply(__instance);
    }
}
