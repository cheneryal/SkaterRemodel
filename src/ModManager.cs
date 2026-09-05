using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkaterMod
{
    public class ModManager : MonoBehaviour
    {
        public static ModManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ModLogger.Log("[ModManager] Singleton instance created.");
            StartCoroutine(VisibilityWatchdog());
        }

        private IEnumerator VisibilityWatchdog()
        {
            ModLogger.LogOnce("watchdog_started", "[ModManager] Visibility Watchdog now monitoring skaters AND goalie parts.");
            while (true)
            {
                yield return new WaitForSeconds(1.0f); 

                var allSkaters = FindObjectsByType<SkaterAnimationBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var skater in allSkaters)
                {
                    if (skater != null && !skater.gameObject.activeInHierarchy)
                    {
                        if (ModLogger.IsDebugMode)
                        {
                            ModLogger.Log($"[Watchdog] FORCE-ACTIVATED inactive skater: {skater.name}");
                        }
                        skater.gameObject.SetActive(true);
                    }
                }

                var goalieParts = FindObjectsByType<ReparentedVanillaPart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var part in goalieParts)
                {
                    if (part != null && !part.gameObject.activeInHierarchy)
                    {
                        if (ModLogger.IsDebugMode)
                        {
                            ModLogger.Log($"[Watchdog] FORCE-REACTIVATING reparented part: {part.name}");
                        }
                        PlayerBody_Spawn_Patch.SetHierarchyActiveAndVisible(part.transform);
                    }
                }
            }
        }
    }
}