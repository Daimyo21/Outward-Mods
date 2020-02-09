﻿using SideLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SharedModConfig
{
    public class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance;

        private readonly string MenuKey = "Shared Mod Config Menu";

        // UI canvas
        private GameObject m_ConfigCanvas;
        private bool m_lastCanvasState;

        private Button m_closeButton;

        private GameObject m_modListHolder;
        private GameObject m_ModListButtonPrefab;

        private GameObject m_SettingsHolder;
        private GameObject m_currentActiveSettings;
        private GameObject m_SettingsPrefab;

        private bool m_initDone;
        public bool IsInitDone()
        {
            return m_initDone;
        }

        #region Internal Setup
        internal void Awake()
        {
            Instance = this;
            StartCoroutine(SetupCoroutine());

            CustomKeybindings.AddAction(MenuKey, CustomKeybindings.KeybindingsCategory.Menus, CustomKeybindings.ControlType.Both, 5, CustomKeybindings.InputActionType.Button);
        }

        internal void Update()
        {
            foreach (SplitPlayer player in SplitScreenManager.Instance.LocalPlayers)
            {
                if (CustomKeybindings.m_playerInputManager[player.RewiredID].GetButtonDown(MenuKey))
                {
                    bool active = m_ConfigCanvas.activeSelf;
                    m_ConfigCanvas.SetActive(!active);
                }
            }

            if (Global.Lobby.PlayersInLobbyCount > 0 && !NetworkLevelLoader.Instance.IsGameplayPaused)
            {
                MenuMouseFix();
            }
        }

        private IEnumerator SetupCoroutine()
        {
            while (!SL.Instance.IsInitDone())
            {
                yield return new WaitForSeconds(0.1f);
            }

            SetupCanvas();

            m_initDone = true;
        }

        private void SetupCanvas()
        {
            // Debug.Log(ModBase.ModName + " started, version: " + ModBase.ModVersion);

            var bundle = SL.Instance.LoadedBundles["sharedmodconfig"];

            if (bundle.LoadAsset("SharedModConfigCanvas") is GameObject canvasAsset)
            {
                m_ConfigCanvas = Instantiate(canvasAsset);
                DontDestroyOnLoad(m_ConfigCanvas);

                var headerHolder = m_ConfigCanvas.transform.Find("Panel").Find("Header_Holder");

                // close button (X)
                m_closeButton = headerHolder.transform.Find("CloseButton").gameObject.GetComponent<Button>();
                m_closeButton.onClick.AddListener(CloseButton);

                // mod list and buttons
                m_modListHolder = headerHolder.Find("ModList_Holder").Find("ModList_Viewport").Find("ModList_Content").gameObject;
                m_ModListButtonPrefab = m_modListHolder.transform.Find("Button_Holder").gameObject;
                var modlistButton = m_ModListButtonPrefab.GetComponent<Button>();
                modlistButton.onClick.AddListener(ModListButton);
                m_ModListButtonPrefab.SetActive(false);

                // mod settings template
                m_SettingsHolder = m_ConfigCanvas.transform.Find("Panel").Find("Settings_Holder").gameObject;

                m_SettingsPrefab = m_SettingsHolder.transform.Find("Settings_Prefab").gameObject;
                m_SettingsPrefab.SetActive(false);

                // disable canvas
                m_ConfigCanvas.SetActive(false);
            }
        }
        #endregion


        #region UI Button Listener Functions
        private void CloseButton()
        {
            m_ConfigCanvas.SetActive(false);
        }

        private void ModListButton()
        {
            var buttontext = EventSystem.current.currentSelectedGameObject.GetComponentInChildren<Text>().text;
            //Debug.Log(buttontext);
            if (m_SettingsPrefab.transform.parent.Find(buttontext) is Transform t)
            {
                if (m_currentActiveSettings)
                {
                    m_currentActiveSettings.SetActive(false);
                }

                t.gameObject.SetActive(true);
                m_currentActiveSettings = t.gameObject;
            }
            else
            {
                Debug.LogError("[SharedModConfig] Error! Could not find transform " + buttontext);
            }
        }
        #endregion


        #region Adding a Config to the UI Menu
        public void AddConfig(ModConfig config)
        {
            // add button to mod list
            var newButton = Instantiate(m_ModListButtonPrefab);
            newButton.name = config.ModName;
            newButton.transform.SetParent(m_modListHolder.transform, false);

            var text = newButton.GetComponentInChildren<Text>();
            text.text = config.ModName;

            var button = newButton.GetComponentInChildren<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ModListButton);

            newButton.SetActive(true);

            // Alphabetize the modlist
            List<Transform> children = new List<Transform>();
            for (int i = m_modListHolder.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = m_modListHolder.transform.GetChild(i);
                children.Add(child);
                child.transform.SetParent(null, false);
            }
            children.Sort((Transform t1, Transform t2) => { return t1.name.CompareTo(t2.name); });
            foreach (Transform child in children)
            {
                child.transform.SetParent(m_modListHolder.transform, false);
            }

            // setup canvas
            var newCanvas = Instantiate(m_SettingsPrefab);
            newCanvas.name = config.ModName;
            newCanvas.transform.SetParent(m_SettingsHolder.transform, false);

            config.m_linkedPanel = newCanvas;

            var title = newCanvas.transform.Find("Title").GetComponent<Text>();
            title.text = config.ModName;

            var contentHolder = newCanvas.transform.Find("SettingsScroll_Holder").Find("Settings_Viewport").Find("Settings_Content");

            var boolPrefab = contentHolder.Find("Toggle_Holder").gameObject;
            var stringPrefab = contentHolder.Find("InputField_Holder").gameObject;
            var floatPrefab = contentHolder.Find("HSlider_Holder").gameObject;
            var titlePrefab = contentHolder.Find("Title_Holder").gameObject;

            // setup settings
            foreach (BBSetting setting in config.Settings)
            {
                switch (setting.GetType().Name)
                {
                    case "BoolSetting":
                    case "SharedModConfig.BoolSetting":
                        SetupBoolSetting(contentHolder, boolPrefab, setting as BoolSetting, titlePrefab);
                        break;
                    case "StringSetting":
                    case "SharedModConfig.StringSetting":
                        SetupStringSetting(contentHolder, stringPrefab, setting as StringSetting, titlePrefab);
                        break;
                    case "FloatSetting":
                    case "SharedModConfig.FloatSetting":
                        SetupFloatSetting(contentHolder, floatPrefab, setting as FloatSetting, titlePrefab);
                        break;
                    default:
                        Debug.LogError("[SharedModConfig] Could not parse BBSetting of type " + setting.GetType().Name);
                        break;
                }
            }

            // cleanup the prefab templates
            Destroy(boolPrefab);
            Destroy(stringPrefab);
            Destroy(floatPrefab);
            Destroy(titlePrefab);
        }

        private GameObject SetupBasicSetting(Transform contentHolder, GameObject prefab, BBSetting setting, GameObject titlePrefab)
        {
            if (!string.IsNullOrEmpty(setting.SectionTitle))
            {
                var newTitle = Instantiate(titlePrefab).gameObject;
                newTitle.transform.SetParent(contentHolder.transform, false);
                var text = newTitle.GetComponent<Text>();
                text.text = setting.SectionTitle;
                newTitle.SetActive(true);
            }

            var newPrefab = Instantiate(prefab);
            newPrefab.transform.SetParent(contentHolder, false);
            setting.LinkedGameObject = newPrefab;
            newPrefab.SetActive(true);

            return newPrefab;
        }

        private void SetupBoolSetting(Transform contentHolder, GameObject prefab, BoolSetting setting, GameObject titlePrefab)
        {
            var newPrefab = SetupBasicSetting(contentHolder, prefab, setting, titlePrefab);

            var Toggle = newPrefab.GetComponentInChildren<Toggle>();
            var Label = newPrefab.GetComponentInChildren<Text>();

            Toggle.isOn = setting.m_value;
            Label.text = string.IsNullOrEmpty(setting.Description) ? setting.Name : setting.Description;
        }

        private void SetupFloatSetting(Transform contentHolder, GameObject prefab, FloatSetting setting, GameObject titlePrefab)
        {
            var newPrefab = SetupBasicSetting(contentHolder, prefab, setting, titlePrefab);

            var label = newPrefab.transform.Find("Label").GetComponent<Text>();
            label.text = string.IsNullOrEmpty(setting.Description) ? setting.Name : setting.Description;

            var slider = newPrefab.GetComponentInChildren<Slider>();
            slider.minValue = setting.MinValue;
            slider.maxValue = setting.MaxValue;
            slider.value = setting.m_value;
            if (setting.RoundTo >= 0)
            {
                slider.value = (float)Math.Round(setting.m_value, setting.RoundTo);
            }

            var textvalue = slider.transform.parent.Find("SliderValue").GetComponent<Text>();
            string s = setting.m_value + (setting.ShowPercent ? "%" : "");
            textvalue.text = s;
        }

        private void SetupStringSetting(Transform contentHolder, GameObject prefab, StringSetting setting, GameObject titlePrefab)
        {
            var newPrefab = SetupBasicSetting(contentHolder, prefab, setting, titlePrefab);

            var Label = newPrefab.transform.Find("Label").GetComponent<Text>();
            Label.text = string.IsNullOrEmpty(setting.Description) ? setting.Name : setting.Description;

            var inputField = newPrefab.GetComponentInChildren<InputField>();
            inputField.text = setting.m_value;
        }
        #endregion


        #region Menu Mouse Fix
        private void MenuMouseFix()
        {
            if (m_lastCanvasState != m_ConfigCanvas.activeSelf)
            {
                m_lastCanvasState = m_ConfigCanvas.activeSelf;

                Character c = CharacterManager.Instance.GetFirstLocalCharacter();

                if (c.CharacterUI.PendingDemoCharSelectionScreen is Panel panel)
                {
                    if (m_lastCanvasState)
                        panel.Show();
                    else
                        panel.Hide();
                }
                else if (m_lastCanvasState)
                {
                    GameObject obj = new GameObject();
                    obj.transform.parent = c.transform;
                    obj.SetActive(true);

                    Panel newPanel = obj.AddComponent<Panel>();
                    At.SetValue(newPanel, typeof(CharacterUI), c.CharacterUI, "PendingDemoCharSelectionScreen");
                    newPanel.Show();
                }
            }
        }
        #endregion
    }
}