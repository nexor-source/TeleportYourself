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
using System;

namespace TeleportYourself
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TeleportYourself : BaseUnityPlugin
    {
        private const string modGUID = "nexor.TeleportYourself";
        private const string modName = "TeleportYourself";
        private const string modVersion = "0.0.2";

        private readonly Harmony harmony = new Harmony(modGUID);

        public ConfigEntry<string> teleportKey;
        public static TeleportYourself Instance;

        public ConfigEntry<float> teleportCooldown; // 传送的冷却时间，单位为秒
        public float lastTeleportTime;

        // 在插件启动时会直接调用Awake()方法
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // 初始化配置项
            teleportKey = Config.Bind<string>("Teleport Yourself Config",
                                               "Teleport Key(传送键)",
                                               "V",
                                               "The key used for teleporting. Letters must be capitalized. If it is an invalid value, the default V is used.(摁哪个键触发传送，字母必须大写，如果是无效值则会使用默认V)");

            teleportCooldown = Config.Bind<float>("Teleport Yourself Config",
                                               "Teleport Cooldown/seconds (传送冷却/秒)",
                                               240,
                                               "Negative numbers have no cooldown (负数则没有冷却)");

            harmony.PatchAll();
            ((TeleportYourself)this).Logger.LogInfo((object)"TeleportYourself 0.0.2 loaded.");
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Update")]
    internal class HUDPatch
    {
        private static Key teleportKey;

        static HUDPatch()
        {
            // 初始设置为一个足够远的时间，以确保第一次传送不受冷却时间限制
            TeleportYourself.Instance.lastTeleportTime = -TeleportYourself.Instance.teleportCooldown.Value;

            // 将配置文件中的字符串表示形式转换为 Key
            if (!(System.Enum.TryParse(TeleportYourself.Instance.teleportKey.Value, out teleportKey)))
            {
                // 如果解析失败，说明用户提供了无效的按键字符串
                // Debug.LogError("Invalid teleport key: [" + TeleportYourself.Instance.teleportKey.Value+"]");
                // Debug.LogError("Using default teleport key [V]");
                teleportKey = Key.V;
            }
        }

        [HarmonyPostfix]
        public static void UpdatePatch()
        {
            // 如果摁下传送键
            if (Keyboard.current[teleportKey].wasPressedThisFrame)
            {
                // 如果传送CD好了
                if (Time.time - TeleportYourself.Instance.lastTeleportTime >= TeleportYourself.Instance.teleportCooldown.Value)
                {
                    // Debug.Log("x started.");

                    List<ShipTeleporter> teleporters = UnityEngine.Object.FindObjectsOfType<ShipTeleporter>().ToList();

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

                        PlayerControllerB old = StartOfRound.Instance.mapScreen.targetedPlayer;
                        StartOfRound.Instance.mapScreen.targetedPlayer = StartOfRound.Instance.localPlayerController;

                        IEnumerator beamUpPlayerCoroutine = (IEnumerator)method.Invoke(teleporters[0], null);
                        teleporters[0].StartCoroutine(beamUpPlayerCoroutine);
                        StartOfRound.Instance.mapScreen.targetedPlayer = old;
                        TeleportYourself.Instance.lastTeleportTime = Time.time;
                    }
                    else
                    {
                        // Debug.LogError("beamUpPlayer method not found");
                    }
                }
                // 如果传送CD没好
                else
                {
                    HUDManager.Instance.DisplayTip("Warning!", "Teleport is on " + (int)(TeleportYourself.Instance.teleportCooldown.Value - Time.time + TeleportYourself.Instance.lastTeleportTime) + " seconds's cooldown. Please wait.");
                    // Debug.Log("Teleport is on cooldown. Please wait.");
                }
            }
            
        }
    }


}
