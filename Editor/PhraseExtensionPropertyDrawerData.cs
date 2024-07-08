using UnityEditor;
using UnityEditor.Localization.UI;
using UnityEngine;

namespace Phrase
{
    [CustomPropertyDrawer(typeof(PhraseExtension))]
    class TablePropertyDrawer : PropertyDrawer
    {
        // public SerializedProperty m_provider;

        // public PhraseProvider Provider => m_provider.objectReferenceValue as PhraseProvider;

        private SerializedProperty m_provider;

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

            m_provider = property.FindPropertyRelative("m_provider");

            EditorGUI.PropertyField(position, m_provider);
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            // add push and pull buttons
            if (m_provider.objectReferenceValue != null)
            {
                // var provider = m_provider.objectReferenceValue as PhraseProvider;
                if (provider != null)
                {
                    var buttonWidth = position.width / 2 - EditorGUIUtility.standardVerticalSpacing;
                    var buttonPosition = position;
                    buttonPosition.width = buttonWidth;
                    if (GUI.Button(buttonPosition, "Push"))
                    {
                        provider.Push();
                    }

                    buttonPosition.x += buttonWidth + EditorGUIUtility.standardVerticalSpacing;
                    if (GUI.Button(buttonPosition, "Pull"))
                    {
                        provider.Pull();
                    }
                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
        }
    }
}