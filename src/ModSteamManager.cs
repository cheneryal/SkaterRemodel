using UnityEngine;
using System.Collections;
using Steamworks;

[DisallowMultipleComponent]
public class ModSteamManager : MonoBehaviour { // Renamed class
	protected static bool s_EverInialized;

	protected static ModSteamManager s_instance; // Renamed
	protected static ModSteamManager Instance {  // Renamed
		get {
			if (s_instance == null) {
				return new GameObject("ModSteamManager").AddComponent<ModSteamManager>(); // Renamed
			}
			else {
				return s_instance;
			}
		}
	}

	public static bool Initialized {
		get {
			return s_EverInialized;
		}
	}

	protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;
	protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText) {
		Debug.LogWarning(pchDebugText);
	}

	protected virtual void Awake() {
		if (s_instance != null) {
			Destroy(gameObject);
			return;
		}
		s_instance = this;
		
		DontDestroyOnLoad(gameObject);

		if (s_EverInialized) {
			throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");
		}

		try {
			if (SteamAPI.RestartAppIfNecessary(SteamUtils.GetAppID())) {
				Application.Quit();
				return;
			}
		}
		catch (System.DllNotFoundException e) {
			Debug.LogError("[SkaterMod Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location.\n" + e, this);
			Application.Quit();
			return;
		}

		s_EverInialized = SteamAPI.Init();
		if (!s_EverInialized) {
			Debug.LogError("[SkaterMod Steamworks.NET] SteamAPI_Init() failed. Refer to the README for more details.", this);
			return;
		}
		
		m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
		SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
	}

	protected virtual void OnDestroy() {
		if (s_instance != this) {
			return;
		}
		s_instance = null;
		if (!s_EverInialized) {
			return;
		}
		SteamAPI.Shutdown();
	}

	protected virtual void Update() {
		if (!s_EverInialized) {
			return;
		}
		SteamAPI.RunCallbacks();
	}
}