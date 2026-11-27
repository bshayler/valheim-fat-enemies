using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace InstantCartographer
{
    /// <summary>
    /// Opening the large map automatically "reads" every Cartography Table the game can see,
    /// merging its stored exploration (and pins) into your map, as if you had walked up and pressed E.
    /// </summary>
    [BepInPlugin(Guid, "Instant Cartographer", "1.0.0")]
    public class InstantCartographerPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.bshayler.instantcartographer";
        private const string TablePrefab = "piece_cartographytable";

        internal static InstantCartographerPlugin Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> MaxDistance;
        internal static ConfigEntry<float> Cooldown;
        internal static ConfigEntry<bool> Notify;
        internal static ConfigEntry<bool> ReadTables;
        internal static ConfigEntry<bool> WriteTables;

        private static float _lastSync = -999f;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Sync Cartography Tables whenever the map is opened.");
            MaxDistance = Config.Bind("General", "MaxDistance", 0f,
                "Only sync tables within this many metres of you. 0 = no limit (every table the game has loaded).");
            Cooldown = Config.Bind("General", "CooldownSeconds", 5f,
                "Minimum seconds between syncs, so toggling the map repeatedly doesn't redo the work.");
            ReadTables = Config.Bind("General", "ReadTables", true,
                "Read: merge what the tables know into your map (the table's 'show/read' side).");
            WriteTables = Config.Bind("General", "WriteTables", true,
                "Write: publish your exploration and pins to the tables so others can read them (the table's 'place markings' side).");
            Notify = Config.Bind("General", "ShowMessage", true, "Show a message when a sync merged table data.");
            new Harmony(Guid).PatchAll(typeof(InstantCartographerPlugin).Assembly);
        }

        internal static void SyncAll()
        {
            if (!Enabled.Value || Minimap.instance == null || Player.m_localPlayer == null) return;
            if (Time.time - _lastSync < Cooldown.Value) return;
            _lastSync = Time.time;

            var origin = Player.m_localPlayer.transform.position;
            float max = MaxDistance.Value;
            int read = 0, written = 0;
            var zdos = FindTableZdos();

            // 1) READ first, so what we then write contains everyone's data.
            if (ReadTables.Value)
                foreach (var zdo in zdos)
                {
                    if (!InRange(zdo, origin, max)) continue;
                    try
                    {
                        var data = zdo.GetByteArray(ZDOVars.s_data);
                        if (data == null || data.Length == 0) continue;
                        Minimap.instance.AddSharedMapData(data); read++;
                    }
                    catch (Exception e) { Instance.Logger.LogWarning("Read table failed: " + e.Message); }
                }

            // 2) WRITE: publish our (now merged) map to each table.
            if (WriteTables.Value)
            {
                var handled = new HashSet<ZDOID>();
                // Tables with a live object: run the game's own write logic (handles ownership / RPCs).
                var onWrite = AccessTools.Method(typeof(MapTable), "OnWrite");
                foreach (var table in UnityEngine.Object.FindObjectsOfType<MapTable>())
                {
                    var nview = table.GetComponent<ZNetView>();
                    if (nview == null || !nview.IsValid()) continue;
                    if (max > 0f && Vector3.Distance(table.transform.position, origin) > max) continue;
                    try
                    {
                        if (onWrite != null)
                        {
                            onWrite.Invoke(table, new object[] { null, Player.m_localPlayer, null });
                            handled.Add(nview.GetZDO().m_uid); written++;
                        }
                    }
                    catch (Exception e) { Instance.Logger.LogWarning("Write table failed: " + e.Message); }
                }
                // Far-away tables (only present when we hold every ZDO: single-player / host).
                bool authoritative = ZNet.instance != null && ZNet.instance.IsServer();
                foreach (var zdo in zdos)
                {
                    if (handled.Contains(zdo.m_uid) || !InRange(zdo, origin, max)) continue;
                    if (!authoritative && !zdo.IsOwner()) continue;
                    try
                    {
                        var merged = Minimap.instance.GetSharedMapData(zdo.GetByteArray(ZDOVars.s_data));
                        if (merged == null) continue;
                        if (!zdo.IsOwner()) zdo.SetOwner(ZDOMan.GetSessionID());
                        zdo.Set(ZDOVars.s_data, merged); written++;
                    }
                    catch (Exception e) { Instance.Logger.LogWarning("Write table failed: " + e.Message); }
                }
            }

            if ((read > 0 || written > 0) && Notify.Value)
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    "Cartography synced (read " + read + ", wrote " + written + ")");
        }

        private static bool InRange(ZDO zdo, Vector3 origin, float max) =>
            zdo != null && (max <= 0f || Vector3.Distance(zdo.GetPosition(), origin) <= max);

        /// <summary>
        /// Single-player and hosts hold every ZDO in memory, so all tables are found.
        /// Clients on a dedicated server only hold ZDOs near them, so only nearby tables are found.
        /// </summary>
        private static List<ZDO> FindTableZdos()
        {
            var seen = new HashSet<ZDOID>();
            var result = new List<ZDO>();

            if (ZDOMan.instance != null)
            {
                try
                {
                    var all = new List<ZDO>();
                    ZDOMan.instance.GetAllZDOsWithPrefab(TablePrefab, all);
                    foreach (var z in all) if (z != null && seen.Add(z.m_uid)) result.Add(z);
                }
                catch (Exception e) { Instance.Logger.LogDebug("ZDO prefab lookup failed: " + e.Message); }
            }

            // Fallback / complement: any table with a live object in the scene.
            foreach (var table in UnityEngine.Object.FindObjectsOfType<MapTable>())
            {
                var nview = table.GetComponent<ZNetView>();
                var zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
                if (zdo != null && seen.Add(zdo.m_uid)) result.Add(zdo);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    internal static class MapOpenPatch
    {
        private static void Postfix(Minimap.MapMode mode)
        {
            if (mode == Minimap.MapMode.Large) InstantCartographerPlugin.SyncAll();
        }
    }

    /// <summary>Adds an on/off toggle to the large map, stacked just above the vanilla "publish position" toggle.</summary>
    [HarmonyPatch(typeof(Minimap), "Start")]
    internal static class MapToggleBuilder
    {
        internal static Toggle Toggle;

        private static void Postfix(Minimap __instance)
        {
            try
            {
                var source = __instance.m_publicPosition;
                if (source == null) return;
                var go = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                go.name = "InstantCartographerToggle";
                foreach (var loc in go.GetComponentsInChildren<Localize>(true)) UnityEngine.Object.Destroy(loc);
                var label = go.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "Instant Cartographer";

                var srcRect = (RectTransform)source.transform;
                var rect = (RectTransform)go.transform;
                rect.anchoredPosition = srcRect.anchoredPosition + new Vector2(0f, srcRect.rect.height + 6f);

                Toggle = go.GetComponent<Toggle>();
                Toggle.onValueChanged.RemoveAllListeners();
                Toggle.SetIsOnWithoutNotify(InstantCartographerPlugin.Enabled.Value);
                Toggle.onValueChanged.AddListener(on => InstantCartographerPlugin.Enabled.Value = on);
            }
            catch (Exception e) { InstantCartographerPlugin.Instance.Logger.LogWarning("Could not add map toggle: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    internal static class MapToggleRefresh
    {
        // Keep the checkbox in step with the config file if it was edited while playing.
        private static void Postfix()
        {
            if (MapToggleBuilder.Toggle != null)
                MapToggleBuilder.Toggle.SetIsOnWithoutNotify(InstantCartographerPlugin.Enabled.Value);
        }
    }
}
