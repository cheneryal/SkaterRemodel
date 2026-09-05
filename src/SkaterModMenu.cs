using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UITK = UnityEngine.UIElements;

namespace SkaterMod
{
    public sealed class SkaterModMenu : MonoBehaviour
    {
        private static SkaterModMenu _instance;

        public SkaterModMenu()
        {
            _instance = this;
        }

        private UITK.VisualElement _panel;
        private UITK.VisualElement _backdrop;
        private UITK.Button _mainBtn, _pauseBtn;
        private UITK.VisualElement _lastRoot;
        private UITK.VisualElement _settingsSectionVE;

        private UITK.UIDocument _doc;

        private float _nextUIProbeAt = 0f;
        private bool _buttonsWiredForThisRoot;

        private bool _savedCursorState = false;
        private CursorLockMode _prevLockState = CursorLockMode.None;
        private bool _prevCursorVisible = false;

        private static readonly Color32 RowBg = new Color32(61, 61, 61, 255);
        private static readonly Color32 ButtonBg = new Color32(57, 57, 57, 255);
        private static Font _uiTextFont;

        private static Font GetUIFont()
        {
            if (_uiTextFont != null) return _uiTextFont;
            try { _uiTextFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return _uiTextFont;
        }

        private static void MakeReadable(UITK.Label l)
        {
            l.style.color = Color.white;
            l.style.unityFont = GetUIFont();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            try
            {
                _panel?.RemoveFromHierarchy();
                _backdrop?.RemoveFromHierarchy();
                _mainBtn?.RemoveFromHierarchy();
                _pauseBtn?.RemoveFromHierarchy();
            }
            catch { }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextUIProbeAt) return;
            _nextUIProbeAt = Time.unscaledTime + 0.75f;

            try
            {
                var uiMgr = MonoBehaviourSingleton<UIManager>.Instance;
                _doc = uiMgr != null ? uiMgr.UIDocument : FindFirstObjectByType<UITK.UIDocument>(FindObjectsInactive.Include);
                var root = _doc != null ? _doc.rootVisualElement : null;
                if (root == null) return;

                if (_lastRoot != root)
                {
                    _lastRoot = root;
                    _buttonsWiredForThisRoot = false;
                    _backdrop?.RemoveFromHierarchy();
                    _panel?.RemoveFromHierarchy();
                    _backdrop = null;
                    _panel = null;
                }

                if (!_buttonsWiredForThisRoot) TryWireButtonsOnce(root);
                if (_panel == null) BuildPanel(root);
            }
            catch (Exception e) { Debug.LogException(e); }

            if (_panel != null && _panel.style.display == UITK.DisplayStyle.Flex)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    ClosePanel();
                }
            }
        }

        private static readonly FieldInfo _fiMainSettings =
            typeof(UIMainMenu).GetField("settingsButton", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _fiPauseSettings =
            typeof(UIPauseMenu).GetField("settingsButton", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void CopyClasses(UITK.VisualElement from, UITK.VisualElement to)
        {
            if (from == null || to == null) return;
            foreach (var cls in from.GetClasses()) to.AddToClassList(cls);
        }

        private static UITK.Button MakeSiblingButton(UITK.Button reference, string text, Action onClick)
        {
            var b = new UITK.Button(onClick) { text = text };
            CopyClasses(reference, b);
            b.name = text.Replace(" ", "_") + "_SkaterMod";
            b.pickingMode = UITK.PickingMode.Position;

            b.style.backgroundColor = new UITK.StyleColor(ButtonBg);
            b.style.unityTextAlign = new UITK.StyleEnum<TextAnchor>(TextAnchor.MiddleLeft);
            b.style.width = reference.style.width;
            b.style.height = reference.style.height;
            b.style.marginTop = 8f;
            b.style.paddingLeft = 15f;
            return b;
        }

        private void TryWireButtonsOnce(UITK.VisualElement root)
        {
            if (root == null || _buttonsWiredForThisRoot) return;

            var uiMgr = MonoBehaviourSingleton<UIManager>.Instance;
            var main = uiMgr?.MainMenu;
            var pause = uiMgr?.PauseMenu;

            if (main != null && _fiMainSettings != null)
            {
                var refBtn = _fiMainSettings.GetValue(main) as UITK.Button;
                if (refBtn?.parent != null && refBtn.parent.Query<UITK.Button>("PLAYER_MODEL_SkaterMod").First() == null)
                {
                    _mainBtn = MakeSiblingButton(refBtn, "PLAYER MODEL", OpenPanel);
                    int insertAt = Math.Min(4, refBtn.parent.childCount);
                    refBtn.parent.Insert(insertAt, _mainBtn);
                }
            }
            if (pause != null && _fiPauseSettings != null)
            {
                var refBtn = _fiPauseSettings.GetValue(pause) as UITK.Button;
                if (refBtn?.parent != null && refBtn.parent.Query<UITK.Button>("PLAYER_MODEL_SkaterMod").First() == null)
                {
                    _pauseBtn = MakeSiblingButton(refBtn, "PLAYER MODEL", OpenPanel);
                    int insertAt = Math.Min(1, refBtn.parent.childCount);
                    refBtn.parent.Insert(insertAt, _pauseBtn);
                }
            }

            if ((_mainBtn != null) || (_pauseBtn != null))
            {
                _buttonsWiredForThisRoot = true;
                ModLogger.Log("SkaterMod Menu button hooked.");
            }
        }

        public void OpenPanel()
        {
            if (_panel == null) { TryWireButtonsOnce(_lastRoot); if (_panel == null) return; }

            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (ui != null)
            {
                _prevLockState = UnityEngine.Cursor.lockState;
                _prevCursorVisible = UnityEngine.Cursor.visible;
                _savedCursorState = true;

                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _backdrop.style.display = UITK.DisplayStyle.Flex;
            _panel.style.display = UITK.DisplayStyle.Flex;
            _panel.BringToFront();
        }

        public void ClosePanel()
        {
            if (_panel == null) return;

            _backdrop.style.display = UITK.DisplayStyle.None;
            _panel.style.display = UITK.DisplayStyle.None;

            var ui = MonoBehaviourSingleton<UIManager>.Instance;
            if (_savedCursorState)
            {
                UnityEngine.Cursor.lockState = _prevLockState;
                UnityEngine.Cursor.visible = _prevCursorVisible;
                _savedCursorState = false;
            }
        }

        private void BuildPanel(UITK.VisualElement root)
        {
            _backdrop = new UITK.VisualElement { name = "SkaterMod_Backdrop" };
            _backdrop.style.position = UITK.Position.Absolute;
            _backdrop.style.left = 0; _backdrop.style.top = 0; _backdrop.style.right = 0; _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            _backdrop.style.display = UITK.DisplayStyle.None;
            _backdrop.pickingMode = UITK.PickingMode.Position;
            _backdrop.RegisterCallback<UITK.PointerUpEvent>(_ => ClosePanel());
            root.Add(_backdrop);

            _panel = new UITK.VisualElement { name = "SkaterMod_Panel" };
            _panel.style.position = UITK.Position.Absolute;
            _panel.style.left = new UITK.Length(50, UITK.LengthUnit.Percent);
            _panel.style.top = new UITK.Length(50, UITK.LengthUnit.Percent);
            _panel.style.translate = new UITK.Translate(
                new UITK.Length(-50, UITK.LengthUnit.Percent),
                new UITK.Length(-50, UITK.LengthUnit.Percent), 0f);
            _panel.style.width = 800;
            _panel.style.height = new UITK.Length(70, UITK.LengthUnit.Percent);
            _panel.style.backgroundColor = new StyleColor(new Color32(30, 30, 30, 255));
            _panel.style.paddingLeft = 8; _panel.style.paddingRight = 8;
            _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8;
            _panel.style.display = UITK.DisplayStyle.None;
            root.Add(_panel);

            var bigTitle = new UITK.Label("PLAYER MODEL SETTINGS");
            bigTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            bigTitle.style.fontSize = 30;
            bigTitle.style.color = Color.white;
            bigTitle.style.marginBottom = 16;
            _panel.Add(bigTitle);

            var scroll = new UITK.ScrollView
            {
                verticalScrollerVisibility = UITK.ScrollerVisibility.Auto,
                horizontalScrollerVisibility = UITK.ScrollerVisibility.Hidden
            };
            scroll.style.flexGrow = 1;
            _panel.Add(scroll);

            _settingsSectionVE = new UITK.VisualElement();
            scroll.Add(_settingsSectionVE);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.Center;
            buttonContainer.style.alignItems = Align.Center;
            buttonContainer.style.marginTop = 8;
            _panel.Add(buttonContainer);

            var resetButton = new Button(ResetToDefault) { text = "RESET TO DEFAULT" };
            resetButton.style.height = 50;
            resetButton.style.backgroundColor = new StyleColor(ButtonBg);
            resetButton.style.color = Color.white;
            resetButton.style.fontSize = 20;
            resetButton.style.unityFont = GetUIFont();
            resetButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            resetButton.style.width = 250;
            resetButton.style.marginRight = 8;
            resetButton.RegisterCallback<PointerEnterEvent>(evt => {
                resetButton.style.backgroundColor = Color.white;
                resetButton.style.color = Color.black;
            });
            resetButton.RegisterCallback<PointerLeaveEvent>(evt => {
                resetButton.style.backgroundColor = new StyleColor(ButtonBg);
                resetButton.style.color = Color.white;
            });
            buttonContainer.Add(resetButton);

            var refreshButton = new Button(TriggerTextureRefresh) { text = "REFRESH TEXTURES" };
            refreshButton.style.height = 50;
            refreshButton.style.backgroundColor = new StyleColor(ButtonBg);
            refreshButton.style.color = Color.white;
            refreshButton.style.fontSize = 20;
            refreshButton.style.unityFont = GetUIFont();
            refreshButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            refreshButton.style.width = 250;
            refreshButton.style.marginRight = 8;
            refreshButton.RegisterCallback<PointerEnterEvent>(evt => {
                refreshButton.style.backgroundColor = Color.white;
                refreshButton.style.color = Color.black;
            });
            refreshButton.RegisterCallback<PointerLeaveEvent>(evt => {
                refreshButton.style.backgroundColor = new StyleColor(ButtonBg);
                refreshButton.style.color = Color.white;
            });
            buttonContainer.Add(refreshButton);

            var closeButton = new Button(ClosePanel) { text = "CLOSE" };
            closeButton.style.height = 50;
            closeButton.style.backgroundColor = new StyleColor(ButtonBg);
            closeButton.style.color = Color.white;
            closeButton.style.fontSize = 20;
            closeButton.style.unityFont = GetUIFont();
            closeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            closeButton.style.width = 200;
            closeButton.RegisterCallback<PointerEnterEvent>(evt => {
                closeButton.style.backgroundColor = Color.white;
                closeButton.style.color = Color.black;
            });
            closeButton.RegisterCallback<PointerLeaveEvent>(evt => {
                closeButton.style.backgroundColor = new StyleColor(ButtonBg);
                closeButton.style.color = Color.white;
            });
            buttonContainer.Add(closeButton);

            PopulateSettings();
        }

        private void ResetToDefault()
        {
            var defaultConfig = new ModConfig();
            SkaterModPlugin.Config.CustomTextureRed = defaultConfig.CustomTextureRed;
            SkaterModPlugin.Config.CustomTextureBlue = defaultConfig.CustomTextureBlue;
            SkaterModPlugin.Config.ShowFirstPersonArms = defaultConfig.ShowFirstPersonArms;
            SkaterModPlugin.Config.ShowFirstPersonGloves = defaultConfig.ShowFirstPersonGloves;
            SkaterModPlugin.Config.ShowFirstPersonTorso = defaultConfig.ShowFirstPersonTorso;
            SkaterModPlugin.Config.ShowFirstPersonLegs = defaultConfig.ShowFirstPersonLegs;
            ModConfigManager.SaveConfig();
            SkaterModPlugin.ReloadAndApplyCustomTextures();
            PopulateSettings();
        }

        private void TriggerTextureRefresh()
        {
            ModLogger.Log("UI Refresh Textures button clicked.");
            // Call back to the main plugin to re-scan everything
            SkaterModPlugin.DiscoverAndLoadAllTextures();
            
            // Repopulate the settings in the UI to show the new textures
            PopulateSettings();
        }
        
        private void PopulateSettings()
        {
            _settingsSectionVE.Clear();

            var smLabel = new UITK.Label("Custom texture changes are applied instantly."); MakeReadable(smLabel);
            smLabel.style.marginBottom = 8;
            _settingsSectionVE.Add(smLabel);

            var redTeamDropdown = MakeDropdownRow("RED TEAM TEXTURE", SkaterModPlugin.CustomTextureFiles, SkaterModPlugin.Config.CustomTextureRed, value =>
            {
                SkaterModPlugin.Config.CustomTextureRed = value;
                ModConfigManager.SaveConfig();
                SkaterModPlugin.ReloadAndApplyCustomTextures();
            });
            _settingsSectionVE.Add(redTeamDropdown);

            var blueTeamDropdown = MakeDropdownRow("BLUE TEAM TEXTURE", SkaterModPlugin.CustomTextureFiles, SkaterModPlugin.Config.CustomTextureBlue, value =>
            {
                SkaterModPlugin.Config.CustomTextureBlue = value;
                ModConfigManager.SaveConfig();
                SkaterModPlugin.ReloadAndApplyCustomTextures();
            });
            _settingsSectionVE.Add(blueTeamDropdown);

            var smVisibilityLabel = new UITK.Label("First-person visibility (local player only). Changes apply on next spawn."); MakeReadable(smVisibilityLabel);
            smVisibilityLabel.style.marginTop = 16;
            smVisibilityLabel.style.marginBottom = 8;
            _settingsSectionVE.Add(smVisibilityLabel);

            var showArmsToggle = MakeToggleRow("SHOW ARMS", SkaterModPlugin.Config.ShowFirstPersonArms, on =>
            {
                SkaterModPlugin.Config.ShowFirstPersonArms = on;
                ModConfigManager.SaveConfig();
            });
            _settingsSectionVE.Add(showArmsToggle);

            var showGlovesToggle = MakeToggleRow("SHOW GLOVES", SkaterModPlugin.Config.ShowFirstPersonGloves, on =>
            {
                SkaterModPlugin.Config.ShowFirstPersonGloves = on;
                ModConfigManager.SaveConfig();
            });
            _settingsSectionVE.Add(showGlovesToggle);

            var showTorsoToggle = MakeToggleRow("SHOW TORSO", SkaterModPlugin.Config.ShowFirstPersonTorso, on =>
            {
                SkaterModPlugin.Config.ShowFirstPersonTorso = on;
                ModConfigManager.SaveConfig();
            });
            _settingsSectionVE.Add(showTorsoToggle);

            var showLegsToggle = MakeToggleRow("SHOW LEGS", SkaterModPlugin.Config.ShowFirstPersonLegs, on =>
            {
                SkaterModPlugin.Config.ShowFirstPersonLegs = on;
                ModConfigManager.SaveConfig();
            });
            _settingsSectionVE.Add(showLegsToggle);
        }

        private UITK.VisualElement MakeDropdownRow(string label, System.Collections.Generic.List<string> choices, string currentValue, System.Action<string> onChange)
        {
            var row = new UITK.VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems    = Align.Center,
                    height        = 50,
                    marginBottom  = 8,
                    backgroundColor = new StyleColor(RowBg),
                    paddingLeft   = 12,
                    paddingRight  = 12
                }
            };

            var lab = new UITK.Label(label); MakeReadable(lab);
            lab.style.flexGrow = 1;
            row.Add(lab);

            string defaultValue = choices.Contains(currentValue) ? currentValue : (choices.Count > 0 ? choices[0] : "");
            var dropdown = new UITK.DropdownField(choices, defaultValue);

            dropdown.style.width = 300;

            var textElement = dropdown.Q<UITK.TextElement>();
            if (textElement != null)
            {
                textElement.style.overflow = Overflow.Hidden;
                textElement.style.textOverflow = TextOverflow.Ellipsis;
                textElement.style.whiteSpace = WhiteSpace.NoWrap;
                textElement.style.color = Color.white;
                textElement.style.unityFont = GetUIFont();
            }

            dropdown.RegisterCallback<PointerDownEvent>(evt =>
            {
                dropdown.schedule.Execute(() =>
                {
                    var root = dropdown.panel?.visualTree;
                    if (root == null) return;

                    var popup = root.Q(className: "unity-base-dropdown__container-outer") ?? root.Q(className: "unity-base-popup-field__container");

                    if (popup != null)
                    {
                        popup.Query<Label>().ForEach(l =>
                        {
                            MakeReadable(l);
                            var parent = l.parent;
                            parent.RegisterCallback<PointerEnterEvent>(e => {
                                l.style.color = Color.black;
                            });
                            parent.RegisterCallback<PointerLeaveEvent>(e => {
                                l.style.color = Color.white;
                            });
                        });
                    }
                });
            });

            dropdown.RegisterValueChangedCallback(ev => onChange?.Invoke(ev.newValue));
            row.Add(dropdown);
            return row;
        }

        private UITK.VisualElement MakeToggleRow(string label, bool start, System.Action<bool> onChange)
        {
            var row = new UITK.VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems    = Align.Center,
                    height        = 50,
                    marginBottom  = 8,
                    backgroundColor = new StyleColor(RowBg),
                    paddingLeft   = 12,
                    paddingRight  = 12
                }
            };

            var lab = new UITK.Label(label); MakeReadable(lab);
            lab.style.flexGrow = 1;
            row.Add(lab);

            var t = new UITK.Toggle { value = start };
            t.RegisterValueChangedCallback(ev => onChange?.Invoke(ev.newValue));
            row.Add(t);
            return row;
        }
    }
}