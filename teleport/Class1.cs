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
using System.Security.Cryptography;

namespace TeleportYourself
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TeleportYourself : BaseUnityPlugin
    {
        private const string modGUID = "nexor.TeleportYourself";
        private const string modName = "TeleportYourself";
        private const string modVersion = "0.0.5";

        private readonly Harmony harmony = new Harmony(modGUID);

        public ConfigEntry<string> teleportKey;
        public static TeleportYourself Instance;

        // public ConfigEntry<float> teleportCooldown; // 传送的冷却时间，单位为秒
        public float teleportCooldown = 240;
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

            /*            teleportCooldown = Config.Bind<float>("Teleport Yourself Config",
                                                           "Teleport Cooldown/seconds (传送冷却/秒)",
                                                           240,
                                                           "Negative numbers have no cooldown (负数则没有冷却)");*/

            harmony.PatchAll();
            ((TeleportYourself)this).Logger.LogInfo((object)"TeleportYourself 0.0.5 loaded.");
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Update")]
    internal class HUDPatch
    {
        private static Key teleportKey;

        static HUDPatch()
        {
            // 初始设置为一个足够远的时间，以确保第一次传送不受冷却时间限制
            TeleportYourself.Instance.lastTeleportTime = -TeleportYourself.Instance.teleportCooldown;

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
                if (Time.time - TeleportYourself.Instance.lastTeleportTime >= TeleportYourself.Instance.teleportCooldown)
                {
                    // 启动协程执行延迟操作
                    MonoBehaviour monoBehaviour = MonoBehaviour.FindObjectOfType<MonoBehaviour>();
                    if (monoBehaviour != null)
                    {
                        monoBehaviour.StartCoroutine(DelayedTeleport());
                    }
                    else
                    {
                        // Debug.LogError("Failed to find MonoBehaviour to start coroutine.");
                    }
                }
                // 如果传送CD没好
                else
                {
                    HUDManager.Instance.DisplayTip("Warning!", "Teleport is on " + (int)(TeleportYourself.Instance.teleportCooldown - Time.time + TeleportYourself.Instance.lastTeleportTime) + " seconds's cooldown. Please wait.");
                    // Debug.Log("Teleport is on cooldown. Please wait.");
                }
            }

            /*            // 如果按下了 J 键
                        if (Keyboard.current[Key.J].wasPressedThisFrame)
                        {
                            // 启动协程执行延迟操作
                            MonoBehaviour monoBehaviour = MonoBehaviour.FindObjectOfType<MonoBehaviour>();
                            if (monoBehaviour != null)
                            {
                                monoBehaviour.StartCoroutine(DelayedTeleport());
                            }
                            else
                            {
                                // Debug.LogError("Failed to find MonoBehaviour to start coroutine.");
                            }
                        }*/
        }

        // 延迟操作的协程
        private static IEnumerator DelayedTeleport()
        {
            // 找到所有传送器
            List<ShipTeleporter> teleporters = UnityEngine.Object.FindObjectsOfType<ShipTeleporter>().ToList();
            // 过滤出非反向传送器
            teleporters.RemoveAll(t => t.isInverseTeleporter);

            // 如果没有找到有效传送器，结束协程
            if (teleporters.Count == 0)
            {
                // Debug.Log("Haven't found any valid teleporters.");
                yield break;
            }

            // 成功执行
            TeleportYourself.Instance.lastTeleportTime = Time.time;

            // 获取当前目标玩家和其ID
            PlayerControllerB old = StartOfRound.Instance.mapScreen.targetedPlayer;
            int old_id = 0, my_id = 0;
            for (int i = 0; i < StartOfRound.Instance.mapScreen.radarTargets.Count; i++)
            {
                PlayerControllerB now = StartOfRound.Instance.mapScreen.radarTargets[i].transform.gameObject.GetComponent<PlayerControllerB>();
                if (now == StartOfRound.Instance.localPlayerController)
                {
                    my_id = i;
                }
                if (now == old)
                {
                    old_id = i;
                }
            }
            // Debug.Log("Found old_id , my_id: " + old_id + ", " + my_id);

            // 向服务器发送转换为 my_id 的请求
            StartOfRound.Instance.mapScreen.SwitchRadarTargetServerRpc(my_id);

            // 等待服务器响应完成
            yield return new WaitUntil(() => StartOfRound.Instance.mapScreen.targetedPlayer == StartOfRound.Instance.localPlayerController);

            // 执行传送器操作
            teleporters[0].PressTeleportButtonOnLocalClient();

            // 等待一段时间，保证传送完成
            // yield return new WaitForSeconds(1f);

            // 向服务器发送转换为 old_id 的请求
            StartOfRound.Instance.mapScreen.SwitchRadarTargetServerRpc(old_id);
        }

    }


    [HarmonyPatch(typeof(StartOfRound), "Awake")]
    internal class StartOfRound_Awake_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Debug.Log("PLAYER HAS NOW AWAKEN!!!!!");

            TeleportYourself.Instance.lastTeleportTime = -TeleportYourself.Instance.teleportCooldown;

        }

    }
}