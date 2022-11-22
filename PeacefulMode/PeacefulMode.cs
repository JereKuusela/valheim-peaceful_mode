using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace PeacefulMode;
[HarmonyPatch]
[BepInPlugin(GUID, NAME, VERSION)]
public class PeacefulMode : BaseUnityPlugin
{
  const string GUID = "peaceful_mode";
  const string NAME = "Peaceful Mode";
  const string VERSION = "1.1";
#nullable disable
  public static ConfigEntry<bool> allowHunting;
#nullable enable
  public static ServerSync.ConfigSync ConfigSync = new(GUID)
  {
    DisplayName = NAME,
    CurrentVersion = VERSION,
    IsLocked = true,
    ModRequired = true
  };
  public void Awake()
  {
    new Harmony(GUID).PatchAll();
    allowHunting = config("1. General", "Allow hunting", false, "Whether deer (or similar modded creatures) can be hunted.");
  }

  ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
  {
    ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

    SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
    syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

    return configEntry;
  }

  ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

  static bool IsIgnored(MonoBehaviour obj) => allowHunting.Value && obj.GetComponent<AnimalAI>();

  [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character)), HarmonyPostfix]
  static bool NoAggro1(bool result, Character a, Character b) => (IsIgnored(a) || IsIgnored(b)) ? result : false;

  [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget)), HarmonyPrefix]
  static bool NoAggro2() => false;

  [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy)), HarmonyPostfix]
  static Character? NoAggro3(Character result, BaseAI __instance) => IsIgnored(__instance) ? result : null;

  [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage)), HarmonyPrefix]
  static bool NoDamage(Character __instance) => IsIgnored(__instance);

  [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.Awake)), HarmonyPostfix]
  static void CreatureNodes(BaseAI __instance)
  {
    if (__instance.GetComponent<Pickable>()) return;
    if (IsIgnored(__instance)) return;
    var p = __instance.gameObject.AddComponent<Pickable>();
    p.m_respawnTimeMinutes = 30;
  }

  [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverName)), HarmonyPrefix]
  static bool LootHoverName(Pickable __instance, ref string __result)
  {
    if (string.IsNullOrEmpty(__instance.m_overrideName) && !__instance.m_itemPrefab)
    {
      __result = "";
      return false;
    }
    return true;
  }

  [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText)), HarmonyPostfix]
  static string LootHover(string result, Character __instance)
  {
    if (!__instance.TryGetComponent<Pickable>(out var pickable)) return result;
    return (result ?? "") + pickable.GetHoverText();
  }

  [HarmonyPatch(typeof(Pickable), nameof(Pickable.RPC_Pick)), HarmonyPrefix]
  static bool OverridePick(Pickable __instance)
  {
    if (!__instance.TryGetComponent<CharacterDrop>(out var drop)) return true;
    if (!__instance.m_nview.IsOwner()) return true;
    if (__instance.m_picked) return true;
    CharacterDrop.DropItems(drop.GenerateDropList(), __instance.transform.position, 1f);
    __instance.m_nview.InvokeRPC(ZNetView.Everybody, "SetPicked", new object[] { true });
    return false;
  }
  [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact)), HarmonyPrefix]
  static bool OverrideInteract(Tameable __instance, Humanoid user, bool hold, bool alt)
  {
    if (!__instance.TryGetComponent<Pickable>(out var pickable)) return true;
    return !pickable.Interact(user, hold, alt);
  }
}
