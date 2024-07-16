using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.XLIFF;
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

        [SerializeField]
        public List<Project> Projects { get; private set; } = new List<Project>();

        [SerializeField]
        public List<Locale> Locales { get; private set; } = new List<Locale>();

        [SerializeField]
        public List<Locale> LocalesToPull { get; private set; } = new List<Locale>();

        [SerializeField]
        public string m_selectedProjectId = null;

        [SerializeField]
        public string m_selectedLocaleId = null;

        public void FetchProjects()
        {
            Client client = new Client(m_ApiKey, m_ApiUrl);
            Projects = client.ListProjects();
        }

        public void FetchLocales()
        {
            if (m_selectedProjectId == null)
            {
                return;
            }
            Client client = new Client(m_ApiKey, m_ApiUrl);
            Locales = client.ListLocales(m_selectedProjectId);
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
                var phraseExtension = collection.Extensions.FirstOrDefault(e => e is PhraseExtension) as PhraseExtension;
                if (phraseExtension != null)
                {
                    Debug.Log(phraseExtension.m_keyPrefix);
                    // Debug.Log(phraseExtension.m_provider);
                    if (m_selectedLocaleId == null)
                    {
                        Push(collection);
                    }
                    else
                    {
                        var selectedLocale = Locales.FirstOrDefault(l => l.id == m_selectedLocaleId);
                        if (selectedLocale != null)
                        {
                            Push(collection, selectedLocale);
                        }
                    }
                }
            }
        }

        public void PullAll()
        {
            // Debug.Log(LocalizationSettings.AvailableLocales);
        }

        public void Push(StringTableCollection collection)
        {
            Debug.Log("Push");
            Debug.Log(collection);
        }

        public void Push(StringTableCollection collection, Locale locale)
        {
            Debug.Log("Push");
            Debug.Log(collection);
            Debug.Log(locale);

            var matchingStringTable = collection.StringTables.FirstOrDefault(st => st.LocaleIdentifier.Code == locale.code);
            if (matchingStringTable == null)
            {
                Debug.LogError("No matching string table found for locale " + locale.code);
                return;
            }
            const string dir = "Temp/";
            string path = dir + matchingStringTable.name + ".xlf";
            Xliff.Export(matchingStringTable, dir, XliffVersion.V12, new[] { matchingStringTable });
            var xlfContent = File.ReadAllText(path);
            Client client = new Client(m_ApiKey, m_ApiUrl);
            client.UploadFile(xlfContent, m_selectedProjectId, locale.id, false);
            // if (File.Exists(path)) File.Delete(path);
        }

        public void Pull(StringTableCollection collection)
        {
            Debug.Log("Pull");
            Debug.Log(collection);
            foreach(var stringTable in collection.StringTables) {
                // find the locale
                var selectedLocale = Locales.FirstOrDefault(l => l.code == stringTable.LocaleIdentifier.Code);
                if (selectedLocale != null) {
                    Debug.Log("Downloading locale " + selectedLocale.code);
                    Client client = new Client(m_ApiKey, m_ApiUrl);
                    string content = client.DownloadLocale(m_selectedProjectId, selectedLocale.id);
                    using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
                    {
                        Xliff.ImportDocumentIntoTable(XliffDocument.Parse(stream), stringTable);
                    }
                }
            }
        }
    }

    [CustomEditor(typeof(PhraseProvider))]
    public class PhraseEditor : Editor
    {
        bool m_showTables = false;

        bool m_pullOnlySelected = false;

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

            int selectedProjectIndexNew = EditorGUILayout.Popup("Project", selectedProjectIndex, projectNames);
            if (selectedProjectIndexNew != selectedProjectIndex)
            {
                selectedProjectIndex = selectedProjectIndexNew;
                phraseProvider.m_selectedProjectId = phraseProvider.Projects[selectedProjectIndex].id;
                phraseProvider.FetchLocales();

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

            using (new EditorGUI.DisabledScope(selectedProjectIndex < 0))
            {
                if (GUILayout.Button("Push"))
                {
                    phraseProvider.PushAll();
                }

                m_pullOnlySelected = EditorGUILayout.BeginToggleGroup("Pull only selected locales:", m_pullOnlySelected);

                foreach (var locale in phraseProvider.Locales)
                {
                    bool selectedState = phraseProvider.LocalesToPull.Contains(locale);
                    string label = locale.name == locale.code ? locale.name : $"{locale.name} ({locale.code})";
                    bool newSelectedState = EditorGUILayout.ToggleLeft(label, selectedState);
                    if (newSelectedState != selectedState)
                    {
                        if (newSelectedState)
                        {
                            phraseProvider.LocalesToPull.Add(locale);
                        }
                        else
                        {
                            phraseProvider.LocalesToPull.Remove(locale);
                        }
                    }
                }

                EditorGUILayout.EndToggleGroup();
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
