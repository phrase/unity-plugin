using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Phrase
{
    [CustomPropertyDrawer(typeof(PhraseExtension))]
    class TablePropertyDrawer : PropertyDrawer
    {
        private SerializedProperty m_provider;

        private SerializedProperty m_keyPrefix;

        private PhraseProvider provider => m_provider.objectReferenceValue as PhraseProvider;

        private Texture icon;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.yMin += EditorGUIUtility.standardVerticalSpacing;
            position.height = EditorGUIUtility.singleLineHeight;

            icon = EditorGUIUtility.FindTexture("Packages/com.phrase.plugin/Editor/Icons/phrase.png");

            EditorGUI.LabelField(position, EditorGUIUtility.TrTextContent("Phrase", icon), EditorStyles.boldLabel);
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            m_keyPrefix = property.FindPropertyRelative("m_keyPrefix");
            m_provider = property.FindPropertyRelative("m_provider");
            EditorGUI.PropertyField(position, m_provider);
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(position, m_keyPrefix);
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            if (m_provider.objectReferenceValue != null)
            {
                if (provider != null)
                {
                    var buttonWidth = position.width / 2 - EditorGUIUtility.standardVerticalSpacing;
                    var buttonPosition = position;
                    buttonPosition.width = buttonWidth;
                    if (GUI.Button(buttonPosition, "Push"))
                    {
                        var extension = property.GetActualObjectForSerializedProperty<PhraseExtension>(fieldInfo);
                        var collection = extension.TargetCollection as StringTableCollection;
                        EditorCoroutineUtility.StartCoroutineOwnerless(provider.PushAll(new List<StringTableCollection> { collection }));
                    }

                    buttonPosition.x += buttonWidth + EditorGUIUtility.standardVerticalSpacing;
                    if (GUI.Button(buttonPosition, "Pull"))
                    {
                        var extension = property.GetActualObjectForSerializedProperty<PhraseExtension>(fieldInfo);
                        var collection = extension.TargetCollection as StringTableCollection;
                        EditorCoroutineUtility.StartCoroutineOwnerless(provider.Pull(new List<StringTableCollection> { collection }));
                    }
                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 4 + EditorGUIUtility.standardVerticalSpacing * 2;
        }
    }
}