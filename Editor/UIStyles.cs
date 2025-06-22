using UnityEditor;
using UnityEngine;
using System;

namespace OOJUPlugin
{
    public class UIStyles
    {
        // Basic styles
        public GUIStyle headerStyle;
        public GUIStyle subHeaderStyle;
        public GUIStyle buttonStyle;
        public GUIStyle infoBoxStyle;
        public GUIStyle statusBoxStyle;
        public GUIStyle noAssetsStyle;
        public GUIStyle sectionHeaderStyle;
        public GUIStyle subSectionHeaderStyle;
        public GUIStyle iconButtonStyle;
        
        // New styles for improved UI
        public GUIStyle tabStyle;
        public GUIStyle dropAreaStyle;
        public GUIStyle dropAreaActiveStyle;
        public GUIStyle assetGridItemStyle;
        public GUIStyle assetNameStyle;
        public GUIStyle assetTypeStyle;
        public GUIStyle removeButtonStyle;
        public GUIStyle centeredLabelStyle;
        public GUIStyle assetStatusStyle;
        
        // UI images
        public Texture2D headerBackground;
        public Texture2D uploadIcon;
        public Texture2D downloadIcon;
        public Texture2D refreshIcon;
        
        // Colors
        public Color originalBackgroundColor;
        public Color sectionBackgroundColor;
        public Color uploadButtonColor;
        public Color downloadButtonColor;
        public Color tabBackgroundColor;


        public bool IsInitialized { get; private set; } = false;

        public UIStyles()
        {
            originalBackgroundColor = GUI.backgroundColor;
        }

        public void Initialize()
        {
            originalBackgroundColor = GUI.backgroundColor;
            
            // Set colors
            sectionBackgroundColor = EditorGUIUtility.isProSkin 
                ? new Color(0.22f, 0.22f, 0.22f) 
                : new Color(0.85f, 0.85f, 0.85f);
            
            uploadButtonColor = new Color(0.35f, 0.7f, 0.35f, 1.0f);
            downloadButtonColor = new Color(0.35f, 0.5f, 0.8f, 1.0f);
            tabBackgroundColor = EditorGUIUtility.isProSkin 
                ? new Color(0.25f, 0.25f, 0.25f) 
                : new Color(0.8f, 0.8f, 0.8f);
            
            // Header styles
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 20;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.margin = new RectOffset(5, 5, 8, 8);
            
            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionHeaderStyle.fontSize = 16;
            sectionHeaderStyle.alignment = TextAnchor.MiddleLeft;
            sectionHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin 
                ? new Color(0.9f, 0.9f, 0.9f) 
                : new Color(0.2f, 0.2f, 0.2f);
            sectionHeaderStyle.margin = new RectOffset(0, 0, 6, 6);
            sectionHeaderStyle.padding = new RectOffset(10, 10, 5, 5);
            
            subSectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subSectionHeaderStyle.fontSize = 14;
            subSectionHeaderStyle.margin = new RectOffset(10, 10, 8, 4);
            
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subHeaderStyle.fontSize = 13;
            subHeaderStyle.margin = new RectOffset(10, 5, 8, 4);
            
            // Button styles
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(12, 12, 8, 8);
            buttonStyle.margin = new RectOffset(5, 5, 5, 5);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.fontSize = 12;
            buttonStyle.fontStyle = FontStyle.Bold;
            
            iconButtonStyle = new GUIStyle(GUI.skin.button);
            iconButtonStyle.padding = new RectOffset(6, 6, 6, 6);
            iconButtonStyle.fixedWidth = 32;
            iconButtonStyle.fixedHeight = 32;
            
            removeButtonStyle = new GUIStyle(GUI.skin.button);
            removeButtonStyle.fontSize = 16;
            removeButtonStyle.fontStyle = FontStyle.Bold;
            removeButtonStyle.alignment = TextAnchor.MiddleCenter;
            removeButtonStyle.padding = new RectOffset(0, 0, 0, 2);
            
            // Box styles
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
            
            // Tab style
            tabStyle = new GUIStyle(EditorStyles.toolbarButton);
            tabStyle.fontSize = 12;
            tabStyle.fontStyle = FontStyle.Bold;
            tabStyle.fixedHeight = 30;
            tabStyle.margin = new RectOffset(0, 0, 0, 0);
            tabStyle.padding = new RectOffset(8, 8, 6, 6);
            tabStyle.alignment = TextAnchor.MiddleCenter;
            
            // Drag & drop styles
            dropAreaStyle = new GUIStyle(GUI.skin.box);
            dropAreaStyle.alignment = TextAnchor.MiddleCenter;
            dropAreaStyle.fontSize = 14;
            dropAreaStyle.fontStyle = FontStyle.Bold;
            dropAreaStyle.normal.textColor = EditorGUIUtility.isProSkin 
                ? new Color(0.8f, 0.8f, 0.8f) 
                : new Color(0.3f, 0.3f, 0.3f);
            
            dropAreaActiveStyle = new GUIStyle(dropAreaStyle);
            dropAreaActiveStyle.normal.textColor = EditorGUIUtility.isProSkin 
                ? new Color(0.2f, 0.8f, 0.2f) 
                : new Color(0.0f, 0.6f, 0.0f);
            
            // Asset grid styles
            assetGridItemStyle = new GUIStyle(GUI.skin.box);
            assetGridItemStyle.padding = new RectOffset(5, 5, 5, 5);
            assetGridItemStyle.margin = new RectOffset(5, 5, 5, 5);
            assetGridItemStyle.alignment = TextAnchor.UpperCenter;
            
            assetNameStyle = new GUIStyle(EditorStyles.boldLabel);
            assetNameStyle.fontSize = 11;
            assetNameStyle.alignment = TextAnchor.UpperCenter;
            assetNameStyle.wordWrap = true;
            


            assetStatusStyle = new GUIStyle(EditorStyles.miniLabel);
            assetStatusStyle.fontSize = 9;
            assetStatusStyle.alignment = TextAnchor.MiddleCenter;
            assetStatusStyle.fontStyle = FontStyle.Bold;
            
            centeredLabelStyle = new GUIStyle(EditorStyles.label);
            centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            centeredLabelStyle.padding = new RectOffset(10, 10, 20, 20);
            
            // Load icons
            try 
            {
                uploadIcon = EditorGUIUtility.FindTexture("d_UpArrow");
                downloadIcon = EditorGUIUtility.FindTexture("d_DownArrow");
                refreshIcon = EditorGUIUtility.FindTexture("d_Refresh");
            }
            catch (Exception e) 
            {
                Debug.LogWarning("Could not load editor icons: " + e.Message);
            }
            
            IsInitialized = true;
        }
    }
}