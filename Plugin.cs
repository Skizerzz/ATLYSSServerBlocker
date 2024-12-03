using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using System.Linq;

namespace BlockServersByHost {
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        internal static new ManualLogSource Logger;
        private static HashSet<string> _blockedServerNames = new HashSet<string>();
        private static string BlockedServerNamesFileName { get => "blockedServerNames.json"; }
        public static string SteamMatchmakingServerNameKey { get => "name"; }

        public static string[] BlockedServerNames { get => _blockedServerNames.ToArray(); }

        [System.Serializable]
        public class StringArrayWrapper {
            public string[] items;
            public StringArrayWrapper(string[] _items) { items = _items; }
        }

        public static void AddBlockedName(string serverName) {
            if (serverName == null) {
                Logger.LogError("Server name was null");
                return;
            }
            _blockedServerNames.Add(serverName);
            Logger.LogMessage($"{serverName} added to blocked servers list.");
        }

        private bool LoadBlockedServerNames() {
            bool fileExists = File.Exists(BlockedServerNamesFileName);
            FileStream file;
            if (!fileExists) {
                // create the file for later saving
                file = File.OpenWrite(BlockedServerNamesFileName);
                file.Close();
                return false;
            }

            try {
                string fileText = File.ReadAllText(BlockedServerNamesFileName);
                StringArrayWrapper wrapper = UnityEngine.JsonUtility.FromJson<StringArrayWrapper>(fileText);
                string[] serverNames = wrapper.items;
                foreach(string serverName in serverNames) {
                    AddBlockedName(serverName);
                }
            } catch (Exception e) {
                Logger.LogError($"Failed to read {BlockedServerNamesFileName}: {e.Message}");

                // assuming the json is malformed, creating the file blank
                file = File.OpenWrite(BlockedServerNamesFileName);
                file.Close();
                return false;
            }


            return true;
        }

        private void Awake() {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            LoadBlockedServerNames();

            Logger.LogWarning($"To stop blocking server(s), remove the server from '{BlockedServerNamesFileName}' OR delete file '{BlockedServerNamesFileName}' from your ATLYSS root directory to unblock all.");

            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        private void OnDestroy() {
            try {
                StreamWriter streamWriter = new StreamWriter(BlockedServerNamesFileName);

                string[] serverNamesArray = BlockedServerNames.ToArray();
                StringArrayWrapper wrapper = new StringArrayWrapper(serverNamesArray);
                string json = UnityEngine.JsonUtility.ToJson(wrapper);
                streamWriter.Write(json);
                streamWriter.Close();
            } catch (Exception e) {
                Logger.LogError($"Failed to read {BlockedServerNamesFileName}: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(LobbyDataEntry), "Awake")]
        public static class LobbyDataEntry_Awake {
            public static void Postfix(LobbyDataEntry __instance) {
                Button joinLobbyButton = __instance._joinLobbyButton;
                GameObject blockButtonGO = Instantiate(joinLobbyButton.gameObject);
                blockButtonGO.name = "BlockServerButton";
                blockButtonGO.transform.SetParent(joinLobbyButton.gameObject.transform.parent, false);
                Button buttonComponent = blockButtonGO.GetComponent<Button>();
                RectTransform rectTransform = blockButtonGO.GetComponent<RectTransform>();
                blockButtonGO.transform.position = new Vector3(blockButtonGO.transform.position.x - 2, blockButtonGO.transform.position.y + rectTransform.sizeDelta.y - 6, blockButtonGO.transform.position.z);
                Text textComponent = blockButtonGO.GetComponentInChildren<Text>();

                if(textComponent != null) {
                    textComponent.text = "Block Server";
                }

                if(joinLobbyButton != null) {
                    RectTransform joinLobbyButtonRectTransform = joinLobbyButton.GetComponent<RectTransform>();
                    if (joinLobbyButtonRectTransform != null) {
                        rectTransform.sizeDelta = joinLobbyButtonRectTransform.sizeDelta;
                        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x - 4, rectTransform.sizeDelta.y - 12);
                    }
                }

                buttonComponent.onClick.AddListener(() => {
                    OnBlockButtonClick(__instance);
                });
            }

            private static void OnBlockButtonClick(LobbyDataEntry lobbyData) {
                Plugin.AddBlockedName(lobbyData._lobbyName);
                // refresh after adding blocked host
                LobbyListManager._current.Init_RefreshLobbyList();
            }
        }

        [HarmonyPatch(typeof(SteamLobby), nameof(SteamLobby.GetLobbiesList))]
        public static class SteamLobby_GetLobbiesList {
            public static void Prefix(ref SteamLobby __instance) {
                foreach (string lobbyName in Plugin.BlockedServerNames) {
                    SteamMatchmaking.AddRequestLobbyListStringFilter(Plugin.SteamMatchmakingServerNameKey, lobbyName, ELobbyComparison.k_ELobbyComparisonNotEqual);
                }
            }
        }
    }
}