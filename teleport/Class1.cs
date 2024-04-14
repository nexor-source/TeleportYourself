using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace TeleportYourself
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TeleportYourself : BaseUnityPlugin
    {
        private const string modGUID = "nexor.TeleportYourself";
        private const string modName = "TeleportYourself";
        private const string modVersion = "0.0.1";

        private readonly Harmony harmony = new Harmony(modGUID);

/*        public ConfigEntry<bool> my_drop_item;*/
        public static TeleportYourself Instance;

        // 在插件启动时会直接调用Awake()方法
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }


/*            my_drop_item = ((BaseUnityPlugin)this).Config.Bind<bool>("Teleport Yourself Config",
                "will drop items when teleported? 是否在传送后会掉落物品",
                false,
                "仅对自己有效，改为false则不会在传送后掉落物品\n" +
                "Only effective for oneself, changing to false will not drop items after teleportation");*/

            harmony.PatchAll();
            ((TeleportYourself)this).Logger.LogInfo((object)"TeleportYourself 0.0.1 loaded.");
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Update")]
    internal class HUDPatch
    {
        [HarmonyPostfix]
        public static void UpdatePatch()
        {
            if (Keyboard.current[StartKey].wasPressedThisFrame)
            {
                // Debug.Log("x started.");

                List<ShipTeleporter> teleporters = Object.FindObjectsOfType<ShipTeleporter>().ToList();

                // 寻找并删除 isInverseTeleporter 为 True 的项
                teleporters.RemoveAll(t => t.isInverseTeleporter);

                if (teleporters.Count == 0)
                {
                    // Debug.Log("haven't found any valid teleporters.");
                    return;
                }

                // Debug.Log("found teleporters: " + teleporters.Count);

                MethodInfo method = typeof(ShipTeleporter).GetMethod("beamUpPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null)
                {

                    // FieldInfo playerToBeamUpField = typeof(ShipTeleporter).GetField("playerToBeamUp", BindingFlags.Instance | BindingFlags.NonPublic);

                    PlayerControllerB old = StartOfRound.Instance.mapScreen.targetedPlayer;
                    StartOfRound.Instance.mapScreen.targetedPlayer = StartOfRound.Instance.localPlayerController;
                    // playerToBeamUpField.SetValue(teleporters[0], );

                    IEnumerator beamUpPlayerCoroutine = (IEnumerator)method.Invoke(teleporters[0], null);
                    teleporters[0].StartCoroutine(beamUpPlayerCoroutine);
                    StartOfRound.Instance.mapScreen.targetedPlayer = old;
                }
                else
                {
                    // Debug.LogError("beamUpPlayer method not found");
                }
            }
        }

        private static Key StartKey = Key.V;
    }

/*    [HarmonyPatch(typeof(PlayerControllerB), "DropAllHeldItems")]
    internal class PlayerControllerBPatch
    {
        [HarmonyPrefix]
        public static void DropAllHeldItemsPrefix(PlayerControllerB __instance, ref bool itemsFall)
        {
            // 将 itemsFall 参数设为 xxx
            itemsFall = TeleportYourself.Instance.my_drop_item.Value;
        }
    }*/
}
