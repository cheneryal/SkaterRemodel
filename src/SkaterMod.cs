using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections; 
using System.Reflection;
using Newtonsoft.Json;
using Steamworks;

namespace SkaterMod
{
    internal static class ModLogger
    {
        public static bool IsDebugMode = false;
        private static StreamWriter logWriter;
        private static HashSet<string> loggedMessages = new HashSet<string>();

        static ModLogger()
        {
            try
            {
                string puckGamePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string logDirectory = Path.Combine(puckGamePath, "Logs");

                if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);

                string logFilePath = Path.Combine(logDirectory, "SkaterMod.log");
                logWriter = new StreamWriter(logFilePath, false) { AutoFlush = true };
                Log("Logger initialized successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SkaterMod] Failed to initialize file logger: {e.Message}");
                logWriter = null;
            }
        }

        public static void Log(string message)
        {
            Debug.Log($"[SkaterMod] {message}");
            logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        public static void LogOnce(string key, string message)
        {
            if (!loggedMessages.Contains(key))
            {
                loggedMessages.Add(key);
                Log(message);
            }
        }

        public static void Close()
        {
            logWriter?.Close();
            logWriter = null;
        }
    }

    internal class SkaterModMarker : MonoBehaviour
    {
        public PlayerTeam? LastAppliedTeam = null;
    }
    
    internal class ReparentedVanillaPart : MonoBehaviour { }
    
    public class SkaterModPlugin : IPuckPlugin
    {
        private static readonly Harmony harmony = new Harmony("com.yourname.skatermod");

        public static AssetBundle SkaterPackBundle;
        public static GameObject SkaterPrefab;
        public static Material SkaterMaterialRed;
        public static Material SkaterMaterialBlue;
        public static Shader GameShader;
        public static Shader TransparentShader;
        public static Texture RedTeamTexture;
        public static Texture BlueTeamTexture;
        public static Texture DefaultRedTeamTexture;
        public static Texture DefaultBlueTeamTexture;
        public static ModConfig Config;
        public static List<string> CustomTextureFiles = new List<string>();
        public static Dictionary<string, string> CustomTexturePaths = new Dictionary<string, string>();
        private static CallResult<SteamUGCQueryCompleted_t> m_UGCQueryCompleted;
        private const string WORKSHOP_TEXTURE_TAG = "SkaterMod_Texture";
        private static UGCQueryHandle_t m_ActiveUGCQueryHandle;

        public bool OnEnable()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    ModLogger.Log("Dedicated server detected. Skipping SkaterMod loading.");
                    return true;
                }

                ModConfigManager.LoadConfig();
                Config = ModConfigManager.Config;

                string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string bundlePath = Path.Combine(modPath, "skaterpack");

                if (!File.Exists(bundlePath))
                {
                    ModLogger.Log($"ERROR: 'skaterpack' AssetBundle not found at {bundlePath}");
                    return false;
                }

                SkaterPackBundle = AssetBundle.LoadFromFile(bundlePath);
                if (SkaterPackBundle == null) return false;

                new GameObject("ModSteamManager").AddComponent<ModSteamManager>();
                GameObject managerObject = new GameObject("SkaterModManager");
                managerObject.AddComponent<ModManager>();
                
                GameObject menuObject = new GameObject("SkaterModMenu");
                menuObject.AddComponent<SkaterModMenu>();

                SkaterPrefab = SkaterPackBundle.LoadAsset<GameObject>("skater");
                SkaterMaterialRed = SkaterPackBundle.LoadAsset<Material>("Skater_Material_Red");
                SkaterMaterialBlue = SkaterPackBundle.LoadAsset<Material>("Skater_Material_Blue");
                DefaultRedTeamTexture = SkaterMaterialRed.mainTexture;
                DefaultBlueTeamTexture = SkaterMaterialBlue.mainTexture;

                DiscoverAndLoadAllTextures();

                harmony.PatchAll(typeof(SkaterModPlugin).Assembly);
                ModLogger.Log("SkaterMod Enabled and Patches Applied!");
            }
            catch (Exception e)
            {
                ModLogger.Log($"SkaterMod failed to load: {e}");
                return false;
            }
            return true;
        }

        public static void DiscoverAndLoadAllTextures()
        {
            CustomTextureFiles.Clear();
            CustomTexturePaths.Clear();
            CustomTextureFiles.Add("Default");

            // 1. Scan the AssetBundle for built-in custom textures
            if (SkaterPackBundle != null)
            {
                Texture2D[] bundledTextures = SkaterPackBundle.LoadAllAssets<Texture2D>();
                foreach (Texture2D tex in bundledTextures)
                {
                    // Filter by "Custom_" so we don't accidentally grab vanilla material textures
                    if (tex.name.StartsWith("Custom_"))
                    {
                        CustomTextureFiles.Add(tex.name);
                        CustomTexturePaths.Add(tex.name, "BUNDLE"); // Mark as bundled instead of a file path
                        ModLogger.Log($"Found bundled texture: {tex.name}");
                    }
                }
            }

            // 2. Scan Local Folder
            try
            {
                string localCustomTexturePath = Path.Combine(ModConfigManager.ModBaseDirectory, "CustomTextures");
                Directory.CreateDirectory(localCustomTexturePath);
                var localPngFiles = Directory.GetFiles(localCustomTexturePath, "*.png", SearchOption.TopDirectoryOnly);
                foreach (var file in localPngFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (!CustomTexturePaths.ContainsKey(fileName))
                    {
                        CustomTextureFiles.Add(fileName);
                        CustomTexturePaths.Add(fileName, file);
                    }
                }
            }
            catch { }

            LoadPlayerTextures();

            // 3. Scan Steam Workshop
            if (ModSteamManager.Initialized)
            {
                uint numSubscribedItems = SteamUGC.GetNumSubscribedItems();
                if (numSubscribedItems == 0) return;

                PublishedFileId_t[] subscribedItems = new PublishedFileId_t[numSubscribedItems];
                SteamUGC.GetSubscribedItems(subscribedItems, numSubscribedItems);
                
                m_ActiveUGCQueryHandle = SteamUGC.CreateQueryUGCDetailsRequest(subscribedItems, numSubscribedItems);
                if (m_UGCQueryCompleted == null) m_UGCQueryCompleted = CallResult<SteamUGCQueryCompleted_t>.Create(OnUGCQueryCompleted);
                
                SteamAPICall_t apiCall = SteamUGC.SendQueryUGCRequest(m_ActiveUGCQueryHandle);
                m_UGCQueryCompleted.Set(apiCall);
            }
        }

        private static void OnUGCQueryCompleted(SteamUGCQueryCompleted_t pCallback, bool bIOFailure)
        {
            if (bIOFailure || pCallback.m_eResult != EResult.k_EResultOK)
            {
                if (m_ActiveUGCQueryHandle != UGCQueryHandle_t.Invalid) SteamUGC.ReleaseQueryUGCRequest(m_ActiveUGCQueryHandle);
                m_ActiveUGCQueryHandle = UGCQueryHandle_t.Invalid;
                return;
            }

            for (uint i = 0; i < pCallback.m_unNumResultsReturned; i++)
            {
                SteamUGCDetails_t details;
                if (SteamUGC.GetQueryUGCResult(m_ActiveUGCQueryHandle, i, out details))
                {
                    if (details.m_rgchTags.Contains(WORKSHOP_TEXTURE_TAG))
                    {
                        ulong itemSize; string itemDirectory; uint timestamp;
                        if (SteamUGC.GetItemInstallInfo(details.m_nPublishedFileId, out itemSize, out itemDirectory, 1024, out timestamp))
                        {
                            var workshopPngFiles = Directory.GetFiles(itemDirectory, "*.png", SearchOption.AllDirectories);
                            foreach (var file in workshopPngFiles)
                            {
                                string fileName = Path.GetFileName(file);
                                if (!CustomTexturePaths.ContainsKey(fileName))
                                {
                                    CustomTextureFiles.Add(fileName);
                                    CustomTexturePaths.Add(fileName, file);
                                }
                            }
                        }
                    }
                }
            }
            
            SteamUGC.ReleaseQueryUGCRequest(m_ActiveUGCQueryHandle);
            m_ActiveUGCQueryHandle = UGCQueryHandle_t.Invalid;
            ReloadAndApplyCustomTextures();
        }

        public static void LoadPlayerTextures()
        {
            // Changed Texture2D to Texture in the parameters and return type!
            Texture GetTexture(string configName, Texture defaultTex)
            {
                if (configName == "Default" || !CustomTexturePaths.ContainsKey(configName)) return defaultTex;
                
                string path = CustomTexturePaths[configName];
                
                // Load from AssetBundle
                if (path == "BUNDLE")
                {
                    Texture2D bundledTex = SkaterPackBundle.LoadAsset<Texture2D>(configName);
                    if (bundledTex != null) return bundledTex;
                }
                // Load from Hard Drive / Workshop
                else
                {
                    Texture2D diskTex = LoadTextureFromFile(path);
                    if (diskTex != null) return diskTex;
                }
                return defaultTex;
            }

            // Apply Red
            RedTeamTexture = GetTexture(Config.CustomTextureRed, DefaultRedTeamTexture);
            if (RedTeamTexture == DefaultRedTeamTexture) Config.CustomTextureRed = "Default";

            // Apply Blue
            BlueTeamTexture = GetTexture(Config.CustomTextureBlue, DefaultBlueTeamTexture);
            if (BlueTeamTexture == DefaultBlueTeamTexture) Config.CustomTextureBlue = "Default";
        }

        public static void ReloadAndApplyCustomTextures()
        {
            LoadPlayerTextures();

            var allMarkers = UnityEngine.Object.FindObjectsByType<SkaterModMarker>(FindObjectsSortMode.None);
            foreach (var marker in allMarkers)
            {
                marker.LastAppliedTeam = null; // force texture refresh
                PlayerTeam teamToApply = PlayerTeam.Blue;

                Player player = marker.GetComponentInParent<Player>();
                LockerRoomPlayer menuPlayer = marker.GetComponentInParent<LockerRoomPlayer>();

                if (player != null)
                {
                    teamToApply = player.Team;
                }
                else if (menuPlayer != null)
                {
                    PlayerMesh pMesh = Traverse.Create(menuPlayer).Field<PlayerMesh>("playerMesh").Value;
                    if (pMesh != null)
                    {
                        var storage = pMesh.GetComponent<TeamStorage>();
                        if (storage != null) teamToApply = storage.CurrentTeam;
                    }
                }

                ModPlayerPatcher.ApplyTeamLook(marker, teamToApply);
            }
        }

        public bool OnDisable()
        {
            try
            {
                harmony.UnpatchSelf();
                if (SkaterPackBundle != null) SkaterPackBundle.Unload(true); 
                ModLogger.Close();
            }
            catch (Exception e)
            {
                ModLogger.Log($"SkaterMod failed to unpatch: {e.Message}");
                return false;
            }
            return true;
        }

        private static Texture2D LoadTextureFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData)) return texture;
            }
            catch { }
            return null;
        }
    }

    internal class TeamStorage : MonoBehaviour
    {
        public PlayerTeam CurrentTeam = PlayerTeam.Blue;
    }

    internal class VanillaMeshSuppressor : MonoBehaviour
    {
        private bool meshesNuked = false;

        public void HideRenderers()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == "Username" || renderer.gameObject.name == "Number") continue;
                if (renderer.GetComponentInParent<PlayerHead>(true) != null) continue;
                
                // NEW: Protect the Goalie Pads and all their children from being nuked!
                if (renderer.GetComponentInParent<PlayerLegPad>(true) != null || renderer.gameObject.name.Contains("Leg Pad")) continue;
                
                renderer.enabled = false;
                renderer.forceRenderingOff = true;

                if (!meshesNuked)
                {
                    if (renderer is SkinnedMeshRenderer smr) smr.sharedMesh = null;
                    else if (renderer is MeshRenderer)
                    {
                        var mf = renderer.GetComponent<MeshFilter>();
                        if (mf != null) mf.sharedMesh = null;
                    }
                }
            }
            meshesNuked = true;
        }

        void LateUpdate()
        {
            HideRenderers(); 
        }
    }

    [HarmonyPatch(typeof(PlayerBody), "OnNetworkPostSpawn")]
    internal class PlayerBody_Spawn_Patch
    {
        private static readonly HashSet<PlayerBody> patchedInstances = new HashSet<PlayerBody>();

        [HarmonyPostfix]
        static void Postfix(PlayerBody __instance)
        {
            if (patchedInstances.Contains(__instance)) return;

            try
            {
                Player player = __instance.Player;
                if (player == null) return;

                patchedInstances.Add(__instance);
                
                Transform originalPlayerMeshTransform = __instance.transform.Find("Player Mesh");
                if (originalPlayerMeshTransform == null) return;

                Transform playerHeadTransform = DisableVanillaRenderers(originalPlayerMeshTransform, true);
                if (playerHeadTransform == null) return;

                foreach (Renderer r in playerHeadTransform.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

                GameObject newSkater = GameObject.Instantiate(SkaterModPlugin.SkaterPrefab, __instance.transform);
                newSkater.AddComponent<SkaterModMarker>();

                Renderer headRenderer = null;
                foreach (var r in playerHeadTransform.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headRenderer = r;
                        break;
                    }
                }
                if (headRenderer == null) headRenderer = playerHeadTransform.GetComponentInChildren<Renderer>(true);

                if (headRenderer != null)
                {
                    newSkater.AddComponent<SkinToneSync>().Initialize(newSkater.GetComponent<SkaterModMarker>(), headRenderer);
                }

                // Fixed Animation Bridge Initialization
                newSkater.AddComponent<SkaterAnimationBridge>().Initialize(player, __instance, __instance.GetComponent<Movement>());
                newSkater.AddComponent<SkaterIKBridge>().Initialize(player, __instance);

                Transform newHeadBone = PlayerBody_Spawn_Patch.FindChildRecursive(newSkater.transform, "mixamorig:Head");
                if (newHeadBone != null)
                {
                    playerHeadTransform.SetParent(newHeadBone, false);
                    playerHeadTransform.localPosition = Vector3.zero;
                    playerHeadTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    playerHeadTransform.localScale = Vector3.one;
                    
                    playerHeadTransform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    
                    playerHeadTransform.gameObject.SetActive(true);
                }

                Transform usernameTransform = FindChildRecursive(originalPlayerMeshTransform, "Username");
                Transform numberTransform = FindChildRecursive(originalPlayerMeshTransform, "Number");
                Transform newSpineBone = FindChildRecursive(newSkater.transform, "mixamorig:Spine2");

                if (newSpineBone != null)
                {
                    if (usernameTransform != null)
                    {
                        SetHierarchyActiveAndVisible(usernameTransform);
                        usernameTransform.SetParent(newSpineBone, false);
                        string username = player.Username.Value.ToString();
                        int baseLength = 9;
                        Vector3 finalScale = new Vector3(1.25f, 0.5f, 1.0f);
                        if (username.Length > baseLength) finalScale.x *= ((float)baseLength / username.Length);
                        
                        usernameTransform.localPosition = new Vector3(0.001f, 0.09f, -0.204f);
                        usernameTransform.localRotation = Quaternion.Euler(4.874f, 0f, 0f);
                        usernameTransform.localScale = finalScale;
                    }

                    if (numberTransform != null)
                    {
                        SetHierarchyActiveAndVisible(numberTransform);
                        numberTransform.SetParent(newSpineBone, false);
                        numberTransform.localPosition = new Vector3(0.0f, -0.15f, -0.228f);
                        numberTransform.localRotation = Quaternion.Euler(4.874f, 0f, 0f);
                        numberTransform.localScale = new Vector3(0.9f, 0.8f, 1.0f);
                    }
                }
                
                ModPlayerPatcher.ApplyTeamLook(newSkater.GetComponent<SkaterModMarker>(), player.Team);
                
                if (player.IsLocalPlayer)
                {
                    newSkater.AddComponent<LocalPlayerVisibility>().InitializeAndApply();
                }

                __instance.StartCoroutine(DelayedRoleBasedPatch(player, originalPlayerMeshTransform, newSkater));
            }
            catch (Exception e)
            {
                ModLogger.Log($"Critical error in PlayerBody_Spawn_Patch: {e}");
            }
        }

        private static IEnumerator DelayedRoleBasedPatch(Player player, Transform originalPlayerMeshTransform, GameObject newSkater)
        {
            yield return new WaitForSeconds(0.1f);

            if (player.Role == PlayerRole.Goalie)
            {
                Transform legPads = FindChildRecursive(originalPlayerMeshTransform, "Leg Pads");
                if (legPads != null)
                {
                    legPads.gameObject.AddComponent<ReparentedVanillaPart>(); // Keeps Watchdog happy
                    SetHierarchyActiveAndVisible(legPads); // Just turn them on, DO NOT reparent
                }
            }
            else
            {
                Transform legPads = FindChildRecursive(originalPlayerMeshTransform, "Leg Pads");
                if (legPads != null) legPads.gameObject.SetActive(false);
            }
        }
        
        public static void SetHierarchyActiveAndVisible(Transform parent)
        {
            if (parent == null) return;
            parent.gameObject.SetActive(true);
            foreach (Renderer r in parent.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        public static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
        
        public static Transform DisableVanillaRenderers(Transform originalPlayerMeshTransform, bool isGoalie)
        {
            var playerMeshComponent = originalPlayerMeshTransform.GetComponent<PlayerMesh>();
            if (playerMeshComponent == null || playerMeshComponent.PlayerHead == null) return null;

            if (SkaterModPlugin.GameShader == null)
            {
                var renderer = originalPlayerMeshTransform.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    SkaterModPlugin.GameShader = renderer.material.shader;
                }
            }

            Transform headTransform = playerMeshComponent.PlayerHead.transform;
            var suppressor = originalPlayerMeshTransform.GetComponent<VanillaMeshSuppressor>() ?? originalPlayerMeshTransform.gameObject.AddComponent<VanillaMeshSuppressor>();

            return headTransform;
        }
    }

    internal static class ModPlayerPatcher
    {
        public static void ApplyTeamLook(SkaterModMarker marker, PlayerTeam team)
        {
            if (marker == null || marker.LastAppliedTeam == team) return;
            marker.LastAppliedTeam = team;

            Texture textureToApply = (team == PlayerTeam.Blue) ? SkaterModPlugin.BlueTeamTexture : SkaterModPlugin.RedTeamTexture;
            if (textureToApply == null) return;

            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<PlayerHead>(true) != null || 
                    renderer.gameObject.name == "Username" || 
                    renderer.gameObject.name == "Number") continue;

                renderer.enabled = true; 
                
                if (SkaterModPlugin.GameShader != null) renderer.material.shader = SkaterModPlugin.GameShader;
                
                renderer.material.color = Color.white; 
                renderer.material.SetTexture("_BaseMap", textureToApply);
                renderer.material.SetTexture("_MainTex", textureToApply);
            }

            SkinToneSync sync = marker.GetComponent<SkinToneSync>();
            if (sync != null) sync.ForceRefresh();
        }
    }

    [HarmonyPatch(typeof(PlayerMesh), "SetJerseyID")]
    internal class PlayerMesh_SetJerseyID_Patch
    {
        [HarmonyPostfix]
        static void Postfix(PlayerMesh __instance, int jerseyID, PlayerTeam team)
        {
            try
            {
                var storage = __instance.gameObject.GetComponent<TeamStorage>() ?? __instance.gameObject.AddComponent<TeamStorage>();
                storage.CurrentTeam = team;

                SkaterModMarker marker = __instance.GetComponentInParent<Player>()?.GetComponentInChildren<SkaterModMarker>()
                                      ?? __instance.GetComponentInParent<LockerRoomPlayer>()?.GetComponentInChildren<SkaterModMarker>();

                if (marker != null) ModPlayerPatcher.ApplyTeamLook(marker, team);
            }
            catch (Exception e)
            {
                ModLogger.Log($"SetJerseyID patch failed: {e}");
            }
        }
    }
    
    [HarmonyPatch]
    internal class LockerRoom_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LockerRoomPlayer), "Start")]
        static void PatchMenuPlayer(LockerRoomPlayer __instance)
        {
            if (__instance.GetComponentInChildren<SkaterModMarker>() != null) return;

            try
            {
                PlayerMesh pMesh = Traverse.Create(__instance).Field<PlayerMesh>("playerMesh").Value;
                if (pMesh == null) return;

                Transform originalPlayerMeshTransform = pMesh.transform;
                Transform playerHeadTransform = PlayerBody_Spawn_Patch.DisableVanillaRenderers(originalPlayerMeshTransform, true);
                if (playerHeadTransform == null) return;
                
                foreach (Renderer r in playerHeadTransform.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                
                GameObject newSkater = GameObject.Instantiate(SkaterModPlugin.SkaterPrefab, __instance.transform);
                var marker = newSkater.AddComponent<SkaterModMarker>();
                newSkater.AddComponent<MenuSkaterHelper>();

                Renderer headRenderer = null;
                foreach (var r in playerHeadTransform.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headRenderer = r;
                        break;
                    }
                }
                if (headRenderer == null) headRenderer = playerHeadTransform.GetComponentInChildren<Renderer>(true);

                if (headRenderer != null)
                {
                    newSkater.AddComponent<SkinToneSync>().Initialize(marker, headRenderer);
                }

                Transform newHeadBone = PlayerBody_Spawn_Patch.FindChildRecursive(newSkater.transform, "mixamorig:Head");
                if (newHeadBone != null)
                {
                    playerHeadTransform.SetParent(newHeadBone, false);
                    playerHeadTransform.localPosition = Vector3.zero;
                    playerHeadTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    playerHeadTransform.localScale = Vector3.one;
                    
                    playerHeadTransform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    
                    playerHeadTransform.gameObject.SetActive(true);
                }
                
                Transform legPads = PlayerBody_Spawn_Patch.FindChildRecursive(originalPlayerMeshTransform, "Leg Pads");
                if (legPads != null)
                {
                    // Ensure they are visible. The game's UI will handle toggling them on/off when changing roles.
                    PlayerBody_Spawn_Patch.SetHierarchyActiveAndVisible(legPads);
                }
                
                var storage = pMesh.gameObject.GetComponent<TeamStorage>();
                PlayerTeam teamToApply = storage != null ? storage.CurrentTeam : PlayerTeam.Blue;
                
                ModPlayerPatcher.ApplyTeamLook(newSkater.GetComponent<SkaterModMarker>(), teamToApply);
            }
            catch (Exception e)
            {
                ModLogger.Log($"Critical error in LockerRoomPlayer_Patch: {e}");
            }
        }
    }
}