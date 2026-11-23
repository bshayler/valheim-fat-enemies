using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FatEnemies
{
    /// <summary>One set of body sliders. Used for the global default and for per-creature overrides.</summary>
    internal class Profile
    {
        public float Belly = 2.2f, Arms = 1.8f, Legs = 1.8f, Head = 1f;
        public float Width = 1.4f, Height = 0.9f, Speed = 0.6f, Anim = 0.6f;

        public Profile Clone() => (Profile)MemberwiseClone();

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        public string Serialize() =>
            string.Join("|", new[] { F(Belly), F(Arms), F(Legs), F(Head), F(Width), F(Height), F(Speed), F(Anim) });

        public static Profile Parse(string[] f, int start)
        {
            float G(int i, float d) =>
                i < f.Length && float.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;
            var p = new Profile();
            p.Belly = G(start, p.Belly); p.Arms = G(start + 1, p.Arms); p.Legs = G(start + 2, p.Legs); p.Head = G(start + 3, p.Head);
            p.Width = G(start + 4, p.Width); p.Height = G(start + 5, p.Height); p.Speed = G(start + 6, p.Speed); p.Anim = G(start + 7, p.Anim);
            return p;
        }
    }

    [BepInPlugin(Guid, "Fat Enemies", "2.0.0")]
    public class FatEnemiesPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.example.fatenemies";
        private const string AllKey = "* All enemies (default)";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> AffectTamed;
        internal static ConfigEntry<bool> AffectBosses;
        private static ConfigEntry<float> _belly, _arms, _legs, _head, _width, _height, _speed, _anim;

        internal static Profile Default = new Profile();
        internal static readonly Dictionary<string, Profile> Overrides = new Dictionary<string, Profile>();
        internal static readonly HashSet<string> Seen = new HashSet<string>();
        private static string _overridesPath;

        internal static Profile GetProfile(string name) =>
            Overrides.TryGetValue(name, out var p) ? p : Default;

        private void Awake()
        {
            Log = Logger;
            Config.SaveOnConfigSet = false; // we save explicitly so dragging a slider doesn't hammer the disk

            Enabled = Config.Bind("General", "Enabled", true, "Turn the mod on or off.");
            AffectTamed = Config.Bind("General", "AffectTamed", false, "Also change tamed creatures.");
            AffectBosses = Config.Bind("General", "AffectBosses", true, "Also change bosses.");

            var d = new Profile();
            _belly = Config.Bind("Default", "BellySize", d.Belly, new ConfigDescription("Torso inflation. 1 = normal.", new AcceptableValueRange<float>(1f, 4f)));
            _arms = Config.Bind("Default", "ArmSize", d.Arms, new ConfigDescription("Arm thickness (across the arm, not its length). 1 = normal.", new AcceptableValueRange<float>(0.5f, 4f)));
            _legs = Config.Bind("Default", "LegSize", d.Legs, new ConfigDescription("Leg thickness (across the leg, not its length). 1 = normal.", new AcceptableValueRange<float>(0.5f, 4f)));
            _head = Config.Bind("Default", "HeadSize", d.Head, new ConfigDescription("Head size. 1 = normal.", new AcceptableValueRange<float>(0.3f, 3f)));
            _width = Config.Bind("Default", "BodyWidth", d.Width, new ConfigDescription("Whole-body sideways and front-to-back stretch. 1 = none.", new AcceptableValueRange<float>(0.5f, 3f)));
            _height = Config.Bind("Default", "BodyHeight", d.Height, new ConfigDescription("Whole-body height multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));
            _speed = Config.Bind("Default", "SpeedMultiplier", d.Speed, new ConfigDescription("Movement speed multiplier. 1 = vanilla.", new AcceptableValueRange<float>(0.1f, 2f)));
            _anim = Config.Bind("Default", "AnimationSpeedMultiplier", d.Anim, new ConfigDescription("Animation speed multiplier (also slows attack wind-ups). 1 = vanilla.", new AcceptableValueRange<float>(0.1f, 2f)));

            Default = new Profile
            {
                Belly = _belly.Value, Arms = _arms.Value, Legs = _legs.Value, Head = _head.Value,
                Width = _width.Value, Height = _height.Value, Speed = _speed.Value, Anim = _anim.Value
            };

            _overridesPath = Path.Combine(Paths.ConfigPath, Guid + ".creatures.txt");
            LoadOverrides();

            new Harmony(Guid).PatchAll();
        }

        // ---------- persistence ----------

        private static void LoadOverrides()
        {
            try
            {
                if (!File.Exists(_overridesPath)) return;
                foreach (var line in File.ReadAllLines(_overridesPath))
                {
                    if (line.StartsWith("#")) continue;
                    var f = line.Split('|');
                    if (f.Length < 2) continue;
                    Overrides[f[0]] = Profile.Parse(f, 1);
                }
            }
            catch (Exception e) { Log.LogWarning("Could not read creature overrides: " + e.Message); }
        }

        private void SaveAll()
        {
            try
            {
                _belly.Value = Default.Belly; _arms.Value = Default.Arms; _legs.Value = Default.Legs; _head.Value = Default.Head;
                _width.Value = Default.Width; _height.Value = Default.Height; _speed.Value = Default.Speed; _anim.Value = Default.Anim;
                Config.Save();

                var lines = new List<string> { "# name|belly|arms|legs|head|width|height|speed|anim (written by the in-game panel)" };
                foreach (var kv in Overrides) lines.Add(kv.Key + "|" + kv.Value.Serialize());
                File.WriteAllLines(_overridesPath, lines.ToArray());
            }
            catch (Exception e) { Log.LogWarning("Could not save settings: " + e.Message); }
            _dirty = false;
        }

        // ---------- in-game panel (IMGUI) ----------

        private bool _open, _dirty, _menuApiBroken;
        private Rect _window = new Rect(20, 80, 640, 620);
        private Vector2 _listScroll, _sliderScroll;
        private string _selected = AllKey;
        private string _filter = "";
        private List<string> _names;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PauseMenuVisible() => Menu.IsVisible();

        private bool InPauseMenu()
        {
            if (_menuApiBroken) return false;
            try { return PauseMenuVisible(); }
            catch (Exception e)
            {
                _menuApiBroken = true;
                Log.LogWarning("Menu.IsVisible unavailable (" + e.GetType().Name + "); press F8 to open the panel instead.");
                return false;
            }
        }

        private void Update()
        {
            try { if (_menuApiBroken && Input.GetKeyDown(KeyCode.F8)) _open = !_open; } catch { }
        }

        private void RefreshNames()
        {
            var set = new HashSet<string>(Seen);
            try
            {
                foreach (var go in ZNetScene.instance.m_prefabs)
                    if (go != null && go.name != "Player" && go.GetComponent<Character>() != null) set.Add(go.name);
            }
            catch { /* fall back to creatures seen so far */ }
            _names = new List<string>(set);
            _names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void OnGUI()
        {
            bool pause = InPauseMenu();
            if (!pause && !(_menuApiBroken && _open))
            {
                if (_open) { _open = false; if (_dirty) SaveAll(); }
                return;
            }

            float s = Mathf.Max(1f, Screen.height / 1080f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float vh = Screen.height / s;

            if (_menuApiBroken)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (pause && GUI.Button(new Rect(20, vh - 60, 190, 40), _open ? "Close Fat Enemies" : "Fat Enemies"))
            {
                _open = !_open;
                if (_open && _names == null) RefreshNames();
                if (!_open && _dirty) SaveAll();
            }

            if (_open)
            {
                if (_names == null) RefreshNames();
                _window.height = Mathf.Min(620, vh - 120);
                // Backing boxes so the pause menu doesn't show through.
                GUI.Box(_window, GUIContent.none);
                GUI.Box(_window, GUIContent.none);
                _window = GUI.Window(0xFA7, _window, DrawWindow, "Fat Enemies");
            }

            if (Event.current.type == EventType.MouseUp && _dirty) SaveAll();
            GUI.matrix = old;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();

            // ---- left: creature list ----
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label("Search");
            _filter = GUILayout.TextField(_filter);
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(_window.height - 120));
            if (GUILayout.Toggle(_selected == AllKey, AllKey, "Button")) _selected = AllKey;
            foreach (var n in _names)
            {
                if (_filter.Length > 0 && n.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string label = (Overrides.ContainsKey(n) ? "★ " : "") + n;
                if (GUILayout.Toggle(_selected == n, label, "Button")) _selected = n;
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Refresh list")) RefreshNames();
            GUILayout.EndVertical();

            // ---- right: sliders ----
            GUILayout.BeginVertical();
            bool all = _selected == AllKey;
            bool hasOverride = !all && Overrides.ContainsKey(_selected);
            GUILayout.Label(all ? "Editing: all enemies without their own settings"
                                : "Editing: " + _selected + (hasOverride ? "  (custom)" : "  (default until you move a slider)"));

            var edit = all ? Default : (hasOverride ? Overrides[_selected] : Default.Clone());
            var before = edit.Serialize();

            bool en = GUILayout.Toggle(Enabled.Value, " Mod enabled");
            if (en != Enabled.Value) { Enabled.Value = en; _dirty = true; }

            _sliderScroll = GUILayout.BeginScrollView(_sliderScroll);
            GUILayout.Label("Body parts");
            edit.Belly = Slider("Belly", edit.Belly, 1f, 4f);
            edit.Arms = Slider("Arms", edit.Arms, 0.5f, 4f);
            edit.Legs = Slider("Legs", edit.Legs, 0.5f, 4f);
            edit.Head = Slider("Head", edit.Head, 0.3f, 3f);
            GUILayout.Space(6);
            GUILayout.Label("Whole body");
            edit.Width = Slider("Width", edit.Width, 0.5f, 3f);
            edit.Height = Slider("Height", edit.Height, 0.5f, 2f);
            GUILayout.Space(6);
            GUILayout.Label("Movement");
            edit.Speed = Slider("Move speed", edit.Speed, 0.1f, 2f);
            edit.Anim = Slider("Animation speed", edit.Anim, 0.1f, 2f);
            GUILayout.EndScrollView();

            if (edit.Serialize() != before)
            {
                if (!all) Overrides[_selected] = edit;
                _dirty = true;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(all ? "Reset defaults" : "Reset to default"))
            {
                if (all) Default = new Profile(); else Overrides.Remove(_selected);
                _dirty = true;
            }
            if (GUILayout.Button("Save")) SaveAll();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200));
            GUILayout.Label(value.ToString("0.00"), GUILayout.Width(44));
            GUILayout.EndHorizontal();
            return Mathf.Round(value * 100f) / 100f;
        }

        private void OnDestroy() { if (_dirty) SaveAll(); }
    }

    /// <summary>Attached to each affected creature. Applies the current profile live every frame.</summary>
    internal class FatMarker : MonoBehaviour
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private string _label;
        private Character _char;
        private Animator _animator;
        private float _lastAnimSet = -1f, _lastAnimFactor = 1f;
        private float _speedApplied = 1f;
        private Vector3 _rootBase, _rootSet;

        private bool _rigSearched;
        private Transform _belly;
        private Vector3 _bellyBase;

        private class Part
        {
            public Transform Bone;
            public Vector3 Base;
            public int Axis = -1;   // limb's long axis, left at 1x; -1 = scale uniformly
            public bool Counter;    // cancel the belly inflation on this bone
            public bool IsHead, IsArm, IsLeg;
        }
        private readonly List<Part> _parts = new List<Part>();

        private void Awake()
        {
            _label = gameObject.name.Replace("(Clone)", "");
            FatEnemiesPlugin.Seen.Add(_label);
            _char = GetComponent<Character>();
            _animator = GetComponentInChildren<Animator>();
            _rootBase = _rootSet = transform.localScale;
        }

        // After the Animator each frame, so our bone scales win over the animation pose.
        private void LateUpdate()
        {
            var p = FatEnemiesPlugin.Enabled.Value ? FatEnemiesPlugin.GetProfile(_label) : null;
            UpdateRoot(p);
            UpdateSpeed(p);
            UpdateAnimation(p);
            UpdateBones(p);
        }

        private void UpdateRoot(Profile p)
        {
            // If the game rescales us (e.g. star level), take its value as the new base.
            if (transform.localScale != _rootSet) _rootBase = transform.localScale;
            var want = p == null ? Vector3.one : new Vector3(p.Width, p.Height, p.Width);
            _rootSet = Vector3.Scale(_rootBase, want);
            transform.localScale = _rootSet;
        }

        private void UpdateSpeed(Profile p)
        {
            if (_char == null) return;
            float speed = p == null ? 1f : p.Speed;
            if (Mathf.Approximately(speed, _speedApplied)) return;
            float ratio = speed / _speedApplied;
            _char.m_walkSpeed *= ratio;
            _char.m_runSpeed *= ratio;
            _char.m_swimSpeed *= ratio;
            _char.m_flySlowSpeed *= ratio;
            _char.m_flyFastSpeed *= ratio;
            _speedApplied = speed;
        }

        private void UpdateAnimation(Profile p)
        {
            if (_animator == null) return;
            float factor = p == null ? 1f : p.Anim;
            float current = _animator.speed;
            // Any speed we didn't set ourselves is the game's new baseline.
            float baseline = Mathf.Approximately(current, _lastAnimSet) ? current / Mathf.Max(_lastAnimFactor, 0.0001f) : current;
            _lastAnimFactor = factor;
            _lastAnimSet = baseline * factor;
            _animator.speed = _lastAnimSet;
        }

        private void UpdateBones(Profile p)
        {
            if (!_rigSearched) FindRig();
            if (_belly == null) return;

            float f = p == null ? 1f : p.Belly;
            _belly.localScale = _bellyBase * f;

            foreach (var part in _parts)
            {
                if (part.Bone == null) continue;
                var v = part.Base;
                if (part.IsArm || part.IsLeg)
                {
                    float t = p == null ? 1f : (part.IsArm ? p.Arms : p.Legs);
                    var thick = new Vector3(t, t, t);
                    if (part.Axis >= 0) thick[part.Axis] = 1f;
                    v = Vector3.Scale(v, thick);
                }
                if (part.IsHead && p != null) v *= p.Head;
                part.Bone.localScale = part.Counter ? v / f : v;
            }
        }

        // ---------- rig discovery ----------

        private static bool NameHas(Transform t, string s) =>
            t.name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;

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

        private static bool IsLimbName(Transform t)
        {
            if (NameHas(t, "armature") || NameHas(t, "armor") || NameHas(t, "harm")) return false;
            return NameHas(t, "arm") || NameHas(t, "leg") || NameHas(t, "thigh");
        }

        private static bool HasLimbAncestor(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (IsLimbName(p)) return true;
            return false;
        }

        // Which local axis points down the bone (toward its child)?
        private static int LongAxis(Transform bone)
        {
            Transform far = null;
            float best = 0f;
            foreach (Transform c in bone)
            {
                float m = c.localPosition.sqrMagnitude;
                if (m > best) { best = m; far = c; }
            }
            if (far == null) return -1;
            var d = far.localPosition;
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            return ax >= ay && ax >= az ? 0 : (ay >= az ? 1 : 2);
        }

        private static void Collect(Transform t, HashSet<Transform> chain)
        {
            chain.Add(t);
            foreach (Transform c in t)
                if (NameHas(c, "spine")) Collect(c, chain);
        }

        private void FindRig()
        {
            _rigSearched = true;
            var all = GetComponentsInChildren<Transform>(true);
            var root = Shallowest(all, "spine") ?? Shallowest(all, "hips", "pelvis");

            if (root == null)
            {
                if (Reported.Add(_label))
                    FatEnemiesPlugin.Log.LogWarning(_label + ": no spine/hips bone found, body effects skipped");
                return;
            }

            // Everything hanging off the torso gets the belly inflation cancelled; the sliders then re-apply.
            var chain = new HashSet<Transform>();
            Collect(root, chain);
            var byBone = new Dictionary<Transform, Part>();
            foreach (var bone in chain)
                foreach (Transform child in bone)
                {
                    if (chain.Contains(child)) continue;
                    var part = new Part
                    {
                        Bone = child, Base = child.localScale, Counter = true,
                        IsHead = NameHas(child, "neck") || NameHas(child, "head")
                    };
                    byBone[child] = part;
                    _parts.Add(part);
                }

            // Arms and legs: thicken across the bone only (topmost bone of each limb).
            foreach (var t in all)
            {
                if (t == transform || !IsLimbName(t) || HasLimbAncestor(t)) continue;
                if (!byBone.TryGetValue(t, out var part))
                {
                    part = new Part { Bone = t, Base = t.localScale };
                    _parts.Add(part);
                }
                part.Axis = LongAxis(t);
                bool leg = NameHas(t, "leg") || NameHas(t, "thigh");
                part.IsLeg = leg;
                part.IsArm = !leg;
            }

            _belly = root;
            _bellyBase = root.localScale;
            if (Reported.Add(_label))
            {
                int arms = 0, legs = 0, heads = 0;
                foreach (var pt in _parts) { if (pt.IsArm) arms++; if (pt.IsLeg) legs++; if (pt.IsHead) heads++; }
                FatEnemiesPlugin.Log.LogInfo(_label + ": belly bone '" + root.name + "', " + arms + " arm(s), " + legs + " leg(s), " + heads + " head/neck");
            }
        }
    }

    internal static class Fatten
    {
        public static void Attach(Character c)
        {
            if (c == null || c.IsPlayer()) return;
            if (!FatEnemiesPlugin.AffectTamed.Value && c.IsTamed()) return;
            if (!FatEnemiesPlugin.AffectBosses.Value && c.IsBoss()) return;
            if (c.GetComponent<FatMarker>() == null) c.gameObject.AddComponent<FatMarker>();
        }
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    internal static class CharacterAwakePatch
    {
        private static void Postfix(Character __instance) => Fatten.Attach(__instance);
    }
}
