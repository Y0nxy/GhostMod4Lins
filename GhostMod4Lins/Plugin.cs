using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Reflection;
using UnityEngine;

namespace GhostMod4Lins
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class GhostMod : BaseUnityPlugin
    {//Todo: fix local character showing up for me when ghost
        public static ConfigEntry<KeyCode> GhostVisibilityToggle;
        public static ConfigEntry<bool> GhostVisiblity;
        public static ConfigEntry<KeyCode> GhostMuteToggle;
        public static ConfigEntry<bool> GhostMute;
        internal static new ManualLogSource Logger;
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} Ver {MyPluginInfo.PLUGIN_VERSION} is loaded!");
            GhostVisibilityToggle = Config.Bind("Ghosts", "GhostVisibilityToggleKey", KeyCode.Comma, "Key to toggle the ghosts' visibility on/off.");
            GhostMuteToggle = Config.Bind("Ghosts", "GhostMuteToggleKey", KeyCode.M, "Key to toggle the ghosts' mute on/off.");
            GhostVisiblity = Config.Bind("Ghosts", "GhostVisibility", true, "Toggle the ghosts' visibility on/off.");
            GhostMute = Config.Bind("Ghosts", "GhostMute", true, "Toggle the ghosts' mute on/off.");
            GhostVisiblity.SettingChanged += (_, _) =>
            {
                Notification("Ghost Visibility is " + (GhostVisiblity.Value ? "ON" : "OFF"));
                ApplyGhostSettingsToAllCharacters();
            };
            GhostMute.SettingChanged += (_, _) =>
            {
                Notification("Ghost Mute is " + (GhostMute.Value ? "ON" : "OFF"));
                ApplyGhostSettingsToAllCharacters();
            };
            harmony.PatchAll(typeof(PlayerGhostPatch));
            harmony.PatchAll(typeof(AnimatedMouthPatch));
        }

        private void Update()
        {
            if (GUIManager.instance != null && GUIManager.instance.windowBlockingInput) return; //no keypress when typing in chat or using menus

            if (Input.GetKeyDown(GhostVisibilityToggle.Value))
            {
                GhostVisiblity.Value = !GhostVisiblity.Value;
            }
            if (Input.GetKeyDown(GhostMuteToggle.Value))
            {
                GhostMute.Value = !GhostMute.Value;
            }
        }

        private static void ApplyGhostSettingsToAllCharacters()
        {
            foreach (Character character in Character.AllCharacters)
            {
                if (character.Ghost != null)
                {
                    ApplyVisibilitySettings(character.Ghost);
                    ApplySoundSettings(character.Ghost);
                }
            }
        }
        internal static void ApplyVisibilitySettings(PlayerGhost ghost)
        {
            bool show = GhostVisiblity.Value;

            if (ghost.PlayerRenderers != null)
                foreach (Renderer r in ghost.PlayerRenderers)
                    if (r != null) r.enabled = show;

            if (ghost.EyeRenderers != null)
                foreach (Renderer r in ghost.EyeRenderers)
                    if (r != null) r.enabled = show;

            if (ghost.mouthRenderer != null)
                ghost.mouthRenderer.enabled = show;

            if (ghost.accessoryRenderer != null)
                ghost.accessoryRenderer.enabled = show;

            Logger.LogDebug($"Ghost '{ghost.m_owner?.characterName}' visibility set to {show}.");
        }
        internal static void ApplySoundSettings(PlayerGhost ghost)
        {
            // Never mute the local player's own ghost voice
            if (ghost.m_owner == null || ghost.m_owner.IsLocal)
            {
                if (ghost.animatedMouth?.audioSource != null)
                    ghost.animatedMouth.audioSource.mute = false;
                return;
            }

            if (ghost.animatedMouth?.audioSource != null)
            {
                ghost.animatedMouth.audioSource.mute = GhostMute.Value;
                Logger.LogDebug($"Ghost '{ghost.m_owner.characterName}' audio muted: {GhostMute.Value}.");
            }
            else
            {
                Logger.LogWarning($"Could not find AnimatedMouth or AudioSource for ghost of {ghost.m_owner?.characterName}.");
            }
        }

        private class PlayerGhostPatch
        {
            [HarmonyPatch(typeof(PlayerGhost), "RPCA_InitGhost")]
            [HarmonyPostfix]
            private static void RPCA_InitGhostPostfix(PlayerGhost __instance, PhotonView character, PhotonView t)
            {
                ApplyVisibilitySettings(__instance);
                ApplySoundSettings(__instance);
            }
        }
        private class AnimatedMouthPatch
        {
            [HarmonyPatch(typeof(AnimatedMouth), "ProcessMicData")]
            [HarmonyPrefix]
            private static bool ProcessMicDataPrefix(AnimatedMouth __instance)
            {
                if (__instance.character == null || __instance.character.Ghost == null)
                    return true;

                if (__instance.character.IsLocal)
                {
                    if (__instance.audioSource != null)
                        __instance.audioSource.mute = false;
                    return true;
                }

                if (GhostMute.Value)
                {
                    if (__instance.audioSource != null)
                        __instance.audioSource.mute = true;
                    return false;
                }

                if (__instance.audioSource != null)
                    __instance.audioSource.mute = false;

                return true;
            }
        }
        
        public static void Notification(string message, string color = "FFFFFF", bool sound = false)
        {
            PlayerConnectionLog connectionLog = UnityEngine.Object.FindAnyObjectByType<PlayerConnectionLog>();
            if (connectionLog == null)
            {
                return;
            }
            string formattedMessage = string.Concat(new string[] { "<color=#", color, ">", message, "</color>" });
            MethodInfo addMessageMethod = typeof(PlayerConnectionLog).GetMethod("AddMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            if (addMessageMethod != null)
            {
                addMessageMethod.Invoke(connectionLog, new object[] { formattedMessage });
                if (connectionLog.sfxJoin != null && sound)
                {
                    connectionLog.sfxJoin.Play(default(Vector3));
                    return;
                }
            }
            else
            {
                Logger.LogMessage("AddMessage method not found.");
            }
        }

    }
}
