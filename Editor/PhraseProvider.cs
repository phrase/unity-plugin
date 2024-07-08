using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using static Phrase.Client;

namespace Phrase
{
    [CreateAssetMenu(fileName = "Phrase", menuName = "Localization/Phrase")]
    public partial class PhraseProvider : ScriptableObject
    {
        [SerializeField]
        public string m_ApiUrl = null;

        [SerializeField]
        public string m_ApiKey;

        public List<Project> Projects { get; private set; } = new List<Project>();

        public void FetchProjects()
        {
            Client client = new Client(m_ApiKey, m_ApiUrl);
            Projects = client.ListProjects();
        }

        public void Push()
        {
            // Push the data to Phrase
        }

        public void Pull()
        {
            // Pull the data from Phrase
        }
    }

    [CustomEditor(typeof(PhraseProvider))]
    public class PhraseEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PhraseProvider phraseProvider = target as PhraseProvider;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiUrl"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiKey"));
            if (GUILayout.Button("Fetch Projects"))
            {
                phraseProvider.FetchProjects();
            }
            string[] projectNames = phraseProvider.Projects.Select(p => p.name).ToArray();
            int selectedProject = EditorGUILayout.Popup("Project", 0, projectNames);

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Push"))
            {
                phraseProvider.Push();
            }

            if (GUILayout.Button("Pull"))
            {
                phraseProvider.Pull();
            }
        }
    }
}
