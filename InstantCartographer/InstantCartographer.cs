using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

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

        private static float _lastSync = -999f;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Sync Cartography Tables whenever the map is opened.");
            MaxDistance = Config.Bind("General", "MaxDistance", 0f,
                "Only sync tables within this many metres of you. 0 = no limit (every table the game has loaded).");
            Cooldown = Config.Bind("General", "CooldownSeconds", 5f,
                "Minimum seconds between syncs, so toggling the map repeatedly doesn't redo the work.");
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
            int merged = 0;

            foreach (var zdo in FindTableZdos())
            {
                if (zdo == null) continue;
                if (max > 0f && Vector3.Distance(zdo.GetPosition(), origin) > max) continue;
                byte[] data;
                try { data = zdo.GetByteArray(ZDOVars.s_data); }
                catch (Exception e) { Instance.Logger.LogWarning("Read table failed: " + e.Message); continue; }
                if (data == null || data.Length == 0) continue;
                try { Minimap.instance.AddSharedMapData(data); merged++; }
                catch (Exception e) { Instance.Logger.LogWarning("Merge table failed: " + e.Message); }
            }

            if (merged > 0 && Notify.Value)
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    "Cartography synced (" + merged + (merged == 1 ? " table)" : " tables)"));
        }

        /// <summary>
        /// Single-player and hosts hold every ZDO in memory, so all tables are found.
        /// Clients on a dedicated server only hold ZDOs near them, so only nearby tables are found.
        /// </summary>
        private static IEnumerable<ZDO> FindTableZdos()
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
}
