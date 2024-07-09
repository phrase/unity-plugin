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
                phraseProvider.PushAll();
            }

            if (GUILayout.Button("Pull"))
            {
                phraseProvider.PullAll();
            }
        }
    }
}
