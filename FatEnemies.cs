using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FatEnemies
{
    [BepInPlugin(Guid, "Fat Enemies", "1.1.0")]
    public class FatEnemiesPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.example.fatenemies";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> BellySize;
        internal static ConfigEntry<float> BodyWidth;
        internal static ConfigEntry<float> BodyHeight;
        internal static ConfigEntry<float> SpeedMultiplier;
        internal static ConfigEntry<float> AnimationSpeedMultiplier;
        internal static ConfigEntry<bool> AffectTamed;
        internal static ConfigEntry<bool> AffectBosses;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Turn the fatness on or off.");
            BellySize = Config.Bind("Fatness", "BellySize", 1.7f,
                new ConfigDescription("How much the torso balloons (spine bone scale). 1 = normal, 1.7 = chonky, 2.5 = absurd. Head, arms and legs keep their size.",
                    new AcceptableValueRange<float>(1f, 4f)));
            BodyWidth = Config.Bind("Fatness", "BodyWidth", 1.0f,
                new ConfigDescription("Extra whole-body sideways stretch. Leave at 1 for the belly look; higher just makes them bigger.",
                    new AcceptableValueRange<float>(0.5f, 6f)));
            BodyHeight = Config.Bind("Fatness", "BodyHeight", 1.0f,
                new ConfigDescription("Whole-body vertical multiplier.", new AcceptableValueRange<float>(0.5f, 3f)));
            SpeedMultiplier = Config.Bind("Slowness", "SpeedMultiplier", 0.6f,
                new ConfigDescription("Movement speed multiplier (walk/run/swim/fly). 1 = vanilla.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            AnimationSpeedMultiplier = Config.Bind("Slowness", "AnimationSpeedMultiplier", 0.6f,
                new ConfigDescription("Animation playback speed multiplier. Also slows attack wind-ups. 1 = vanilla.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            AffectTamed = Config.Bind("General", "AffectTamed", false, "Also fatten tamed creatures.");
            AffectBosses = Config.Bind("General", "AffectBosses", true, "Also fatten bosses.");

            new Harmony(Guid).PatchAll();
        }
    }

    internal class FatMarker : MonoBehaviour
    {
        // Whole-body scale we applied (so re-applying never compounds).
        public Vector3 Applied = Vector3.one;
        public float SpeedApplied = 1f;

        private static readonly HashSet<string> Reported = new HashSet<string>();

        private Animator _animator;
        private float _lastSet = -1f;
        private float _lastFactor = 1f;

        private bool _rigSearched;
        private Transform _belly;
        private Vector3 _bellyBase;
        private readonly List<Transform> _counters = new List<Transform>();
        private readonly List<Vector3> _counterBase = new List<Vector3>();

        private void Awake() => _animator = GetComponentInChildren<Animator>();

        // Runs after the Animator each frame, so our bone scales win over the animation pose.
        private void LateUpdate()
        {
            UpdateAnimationSpeed();
            UpdateBelly();
        }

        private void UpdateAnimationSpeed()
        {
            if (_animator == null) return;
            float factor = FatEnemiesPlugin.Enabled.Value ? FatEnemiesPlugin.AnimationSpeedMultiplier.Value : 1f;
            float current = _animator.speed;
            // Any speed we didn't set ourselves is the game's new baseline.
            float baseline = Mathf.Approximately(current, _lastSet) ? current / Mathf.Max(_lastFactor, 0.0001f) : current;
            _lastFactor = factor;
            _lastSet = baseline * factor;
            _animator.speed = _lastSet;
        }

        private void UpdateBelly()
        {
            if (!_rigSearched) FindRig();
            if (_belly == null) return;

            float f = FatEnemiesPlugin.Enabled.Value ? FatEnemiesPlugin.BellySize.Value : 1f;
            _belly.localScale = _bellyBase * f;
            // Cancel the inflation on everything hanging off the torso (head, arms, legs).
            for (int i = 0; i < _counters.Count; i++)
                if (_counters[i] != null) _counters[i].localScale = _counterBase[i] / f;
        }

        private static bool NameHas(Transform t, string s) =>
            t.name.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { t = t.parent; d++; }
            return d;
        }

        private Transform Shallowest(Transform[] all, params string[] names)
        {
            Transform best = null;
            int bestDepth = int.MaxValue;
            foreach (var t in all)
            {
                if (t == transform) continue;
                foreach (var n in names)
                {
                    if (!NameHas(t, n)) continue;
                    int d = Depth(t);
                    if (d < bestDepth) { best = t; bestDepth = d; }
                    break;
                }
            }
            return best;
        }

        private void FindRig()
        {
            _rigSearched = true;
            var all = GetComponentsInChildren<Transform>(true);
            var root = Shallowest(all, "spine") ?? Shallowest(all, "hips", "pelvis");

            string label = gameObject.name.Replace("(Clone)", "");
            if (root == null)
            {
                if (Reported.Add(label))
                    FatEnemiesPlugin.Log.LogWarning(label + ": no spine/hips bone found, belly effect skipped");
                return;
            }

            var chain = new HashSet<Transform>();
            Collect(root, chain);
            foreach (var bone in chain)
                foreach (Transform child in bone)
                    if (!chain.Contains(child))
                    {
                        _counters.Add(child);
                        _counterBase.Add(child.localScale);
                    }

            _belly = root;
            _bellyBase = root.localScale;
            if (Reported.Add(label))
                FatEnemiesPlugin.Log.LogInfo(label + ": belly bone '" + root.name + "', " + _counters.Count + " counter-scaled");
        }

        private static void Collect(Transform t, HashSet<Transform> chain)
        {
            chain.Add(t);
            foreach (Transform c in t)
                if (NameHas(c, "spine")) Collect(c, chain);
        }
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

            // Optional whole-body stretch (off by default). Undo the previous one first.
            var s = t.localScale;
            var prev = marker.Applied;
            s = new Vector3(s.x / prev.x, s.y / prev.y, s.z / prev.z);
            var body = new Vector3(FatEnemiesPlugin.BodyWidth.Value, FatEnemiesPlugin.BodyHeight.Value, FatEnemiesPlugin.BodyWidth.Value);
            t.localScale = Vector3.Scale(s, body);
            marker.Applied = body;

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

    // Valheim rescales starred/leveled creatures here, so apply again afterward.
    [HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
    internal static class CharacterSetLevelPatch
    {
        private static void Postfix(Character __instance) => Fatten.Apply(__instance);
    }
}
