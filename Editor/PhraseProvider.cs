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

        public string m_selectedProjectId = null;

        public void FetchProjects()
        {
            Client client = new Client(m_ApiKey, m_ApiUrl);
            Projects = client.ListProjects();
        }

        public List<StringTableCollection> AllStringTableCollections()
        {
            return AssetDatabase
                .FindAssets("t:StringTableCollection")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StringTableCollection>)
                .ToList();
        }

        public void PushAll()
        {
            List<StringTableCollection> collections = AllStringTableCollections();
            foreach (StringTableCollection collection in collections)
            {
                Debug.Log(collection);
                // iterate the serialized properties
                foreach (var extension in collection.Extensions)
                {
                    if (extension is PhraseExtension phraseExtension)
                    {
                        Debug.Log(phraseExtension.m_keyPrefix);
                        Debug.Log(phraseExtension.m_provider);
                        // var provider = phraseExtension.m_provider;
                        // provider.Push(collection);
                    }
                }
            }
        }

        public void PullAll()
        {
            // Pull the data from Phrase
        }

        public void Push(StringTableCollection collection)
        {
            Debug.Log("Push");
            Debug.Log(collection);
        }

        public void Pull(StringTableCollection collection)
        {
            Debug.Log("Pull");
            Debug.Log(collection);
        }
    }

    [CustomEditor(typeof(PhraseProvider))]
    public class PhraseEditor : Editor
    {
        bool m_showTables = false;

        public override void OnInspectorGUI()
        {
            PhraseProvider phraseProvider = target as PhraseProvider;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiUrl"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiKey"));
            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Fetch Projects"))
            {
                phraseProvider.FetchProjects();
            }
            string[] projectNames = phraseProvider.Projects.Select(p => p.name).ToArray();
            int selectedProjectIndex = phraseProvider.Projects.FindIndex(p => p.id == phraseProvider.m_selectedProjectId);

            int selectedProject = EditorGUILayout.Popup("Project", selectedProjectIndex, projectNames);
            if (selectedProject >= 0)
            {
                phraseProvider.m_selectedProjectId = phraseProvider.Projects[selectedProject].id;
            }

            m_showTables = EditorGUILayout.BeginFoldoutHeaderGroup(m_showTables, "Connected string tables");
            if (m_showTables)
            {
                List<StringTableCollection> collections = phraseProvider.AllStringTableCollections();
                foreach (StringTableCollection collection in collections)
                {
                    var extension = collection.Extensions.FirstOrDefault(e => e is PhraseExtension) as PhraseExtension;
                    bool selectedState = extension != null && extension.m_provider != null;
                    bool newSelectedState = EditorGUILayout.ToggleLeft(collection.name, selectedState);
                    if (newSelectedState != selectedState)
                    {
                        TogglePhraseExtension(collection, newSelectedState);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(selectedProject < 0))
            {
                if (GUILayout.Button("Push"))
                {
                    phraseProvider.PushAll();
                }

                if (GUILayout.Button("Pull"))
                {
                    phraseProvider.PullAll();
                }
            }
        }

        private void TogglePhraseExtension(StringTableCollection collection, bool selectedState)
        {
            PhraseExtension extension = collection.Extensions.FirstOrDefault(e => e is PhraseExtension) as PhraseExtension;
            if (selectedState)
            {
                if (extension == null)
                {
                    extension = new PhraseExtension();
                    collection.AddExtension(extension);
                }
                extension.m_provider = target as PhraseProvider;
            }
            else
            {
                if (extension != null)
                {
                    collection.RemoveExtension(extension);
                }
            }
        }
    }
}
