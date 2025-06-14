using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using OOJUPlugin;

namespace OOJUPlugin
{
    public class AnimationUI
    {
        private AnimationSettings settings;
        private List<GameObject> pathPoints = new List<GameObject>();

        // ViewModel for animation parameters (for future testability)
        private class AnimationViewModel
        {
            public AnimationType SelectedAnimationType;
            public AnimationCategory SelectedCategory;
            public RelationalAnimationType SelectedRelationalType;
            public GameObject ReferenceObject;
            public List<GameObject> PathPoints = new List<GameObject>();
        }
        private AnimationViewModel viewModel = new AnimationViewModel();

        public AnimationUI()
        {
            settings = AnimationSettings.Instance;
        }

        public void DrawAnimationUI()
        {
            // Null check
            if (settings == null)
            {
                EditorGUILayout.HelpBox("AnimationSettings is not initialized.", MessageType.Error);
                return;
            }
            if (viewModel == null)
            {
                EditorGUILayout.HelpBox("Animation ViewModel is not initialized.", MessageType.Error);
                return;
            }
            if (viewModel.PathPoints == null)
            {
                viewModel.PathPoints = new List<GameObject>();
            }

            // Remove colored section box, use default background
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            try
            {
                GUILayout.Space(10);
                // Unified blue-gray color for section titles
                GUIContent animIcon = EditorGUIUtility.IconContent("Animation Icon");
                GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 15;
                headerStyle.normal.textColor = Color.white;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(animIcon, GUILayout.Width(24), GUILayout.Height(24));
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Animation", headerStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Add animations to selected objects", EditorStyles.miniLabel);
                GUILayout.Space(10);

                // Animation Type Category with colored toolbar
                Color tabColor = new Color(0.27f, 0.40f, 0.47f, 1f);
                GUIStyle toolbarStyle = new GUIStyle(EditorStyles.toolbarButton);
                toolbarStyle.fixedHeight = 28;
                toolbarStyle.fontSize = 12;
                GUI.backgroundColor = tabColor;
                viewModel.SelectedCategory = (AnimationCategory)GUILayout.Toolbar((int)viewModel.SelectedCategory, new string[] { "Independent", "Relational" }, toolbarStyle);
                GUI.backgroundColor = Color.white;
                GUILayout.Space(10);

                if (viewModel.SelectedCategory == AnimationCategory.Independent)
                {
                    DrawIndependentAnimationUI();
                }
                else
                {
                    DrawRelationalAnimationUI();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in DrawAnimationUI: {ex.Message}");
                EditorGUILayout.HelpBox($"Error in DrawAnimationUI: {ex.Message}", MessageType.Error);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        // Dummy implementation for missing methods
        private void DrawIndependentAnimationUI()
        {
            EditorGUILayout.LabelField("DrawIndependentAnimationUI() not implemented.");
        }

        private void DrawRelationalAnimationUI()
        {
            EditorGUILayout.LabelField("DrawRelationalAnimationUI() not implemented.");
        }
    }
} 