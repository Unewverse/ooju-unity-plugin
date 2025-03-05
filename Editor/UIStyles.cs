using UnityEditor;
using UnityEngine;
using System;

namespace OojiCustomPlugin
{
    public class UIStyles
    {
        public GUIStyle headerStyle;
        public GUIStyle subHeaderStyle;
        public GUIStyle buttonStyle;
        public GUIStyle infoBoxStyle;
        public GUIStyle statusBoxStyle;
        public GUIStyle noAssetsStyle;
        public GUIStyle sectionHeaderStyle;
        public GUIStyle iconButtonStyle;
        
        public Texture2D headerBackground;
        public Texture2D uploadIcon;
        public Texture2D downloadIcon;
        public Texture2D refreshIcon;
        
        public Color originalBackgroundColor;
        public Color sectionBackgroundColor;
        public Color uploadButtonColor;
        public Color downloadButtonColor;

        public bool IsInitialized { get; private set; } = false;

        public UIStyles()
        {
            originalBackgroundColor = GUI.backgroundColor;
        }

        public void Initialize()
        {
            originalBackgroundColor = GUI.backgroundColor;
            
            sectionBackgroundColor = EditorGUIUtility.isProSkin 
                ? new Color(0.22f, 0.22f, 0.22f) 
                : new Color(0.85f, 0.85f, 0.85f);
            
            uploadButtonColor = new Color(0.35f, 0.7f, 0.35f, 1.0f);
            downloadButtonColor = new Color(0.35f, 0.5f, 0.8f, 1.0f);
            
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 20;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.margin = new RectOffset(5, 5, 8, 8);
            
            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionHeaderStyle.fontSize = 14;
            sectionHeaderStyle.alignment = TextAnchor.MiddleCenter;
            sectionHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin 
                ? new Color(0.9f, 0.9f, 0.9f) 
                : new Color(0.2f, 0.2f, 0.2f);
            sectionHeaderStyle.margin = new RectOffset(0, 0, 6, 6);
            sectionHeaderStyle.padding = new RectOffset(10, 10, 5, 5);
            
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subHeaderStyle.fontSize = 13;
            subHeaderStyle.margin = new RectOffset(10, 5, 8, 4);
            
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(12, 12, 8, 8);
            buttonStyle.margin = new RectOffset(5, 5, 5, 5);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.fontSize = 12;
            
            iconButtonStyle = new GUIStyle(GUI.skin.button);
            iconButtonStyle.padding = new RectOffset(6, 6, 6, 6);
            iconButtonStyle.fixedWidth = 32;
            iconButtonStyle.fixedHeight = 32;
            
            infoBoxStyle = new GUIStyle(EditorStyles.helpBox);
            infoBoxStyle.padding = new RectOffset(15, 15, 12, 12);
            infoBoxStyle.margin = new RectOffset(5, 5, 5, 5);
            
            statusBoxStyle = new GUIStyle(EditorStyles.helpBox);
            statusBoxStyle.padding = new RectOffset(12, 12, 10, 10);
            statusBoxStyle.margin = new RectOffset(10, 10, 8, 8);
            statusBoxStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            statusBoxStyle.fontSize = 11;
            
            noAssetsStyle = new GUIStyle(EditorStyles.label);
            noAssetsStyle.fontSize = 12;
            noAssetsStyle.alignment = TextAnchor.MiddleCenter;
            noAssetsStyle.padding = new RectOffset(15, 15, 20, 20);
            noAssetsStyle.wordWrap = true;
            
            try {
                uploadIcon = EditorGUIUtility.FindTexture("d_UpArrow");
                downloadIcon = EditorGUIUtility.FindTexture("d_DownArrow");
                refreshIcon = EditorGUIUtility.FindTexture("d_Refresh");
            }
            catch (Exception e) {
                Debug.LogWarning("Could not load editor icons: " + e.Message);
            }
            
            IsInitialized = true;
        }
    }
}