using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.XLIFF;
using UnityEngine.Localization.Settings;
using static Phrase.PhraseClient;
using static Phrase.PhraseOauthAuthenticator;

namespace Phrase
{
    [CreateAssetMenu(fileName = "Phrase", menuName = "Localization/Phrase")]
    public partial class PhraseProvider : ScriptableObject
    {
        [SerializeField]
        public string m_Environment = "EU";

        [SerializeField]
        public string m_ApiUrl = null;

        [SerializeField]
        public bool m_UseOauth = false;

        [System.NonSerialized]
        public bool m_OauthInProgress = false;

        [SerializeField]
        public string m_OauthToken = null;

        [SerializeField]
        public string m_ApiKey;

        [SerializeField]
        public List<Project> Projects { get; private set; } = new List<Project>();

        [SerializeField]
        public List<Locale> Locales { get; private set; } = new List<Locale>();

        [SerializeField]
        public List<string> LocaleIdsToPull { get; private set; } = new List<string>();

        [SerializeField]
        public string m_selectedProjectId = null;

        [SerializeField]
        public string m_selectedLocaleId = null;

        [SerializeField]
        public bool m_pushOnlySelected = false;

        [SerializeField]
        public bool m_pullOnlySelected = false;

        public string Token => m_UseOauth ? m_OauthToken : m_ApiKey;

        private PhraseClient Client => new PhraseClient(this);

        public void Log(string message)
        {
            if (m_Environment == "Custom")
            {
                Debug.Log($"[Phrase] {message}");
            }
        }

        public void LogError(string message)
        {
            if (m_Environment == "Custom")
            {
                Debug.LogError($"[Phrase] {message}");
            }
        }

        public void SetOauthToken(string token)
        {
            m_OauthToken = token;
            m_OauthInProgress = false;
            FetchProjects();
        }

        public async Task<bool> RefreshToken()
        {
            if (m_UseOauth)
            {
                return await PhraseOauthAuthenticator.RefreshToken();
            }
            return false;
        }

        public bool IsProjectSelected => Projects.Count > 0 && m_selectedProjectId != null;

        public bool HasLocaleMismatch => MissingLocalesLocally().Count > 0 || MissingLocalesRemotely().Count > 0;

        public async void FetchProjects()
        {
            Projects = await Client.ListProjects();
        }

        public async void FetchLocales()
        {
            if (m_selectedProjectId == null)
            {
                return;
            }
            Locales = await Client.ListLocales(m_selectedProjectId);
            LocaleIdsToPull.Clear();
        }

        public List<StringTableCollection> AllStringTableCollections()
        {
            return AssetDatabase
                .FindAssets("t:StringTableCollection")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StringTableCollection>)
                .ToList();
        }

        public List<StringTableCollection> ConnectedStringTableCollections()
        {
            return AllStringTableCollections()
                .Where(collection => collection.Extensions.Any(e => e is PhraseExtension))
                .ToList();
        }

        public List<UnityEngine.Localization.Locale> MissingLocalesRemotely()
        {
            return LocalizationSettings.AvailableLocales.Locales
                .Where(l => !Locales.Any(pl => pl.code == l.Identifier.Code))
                .ToList();
        }

        public List<Locale> MissingLocalesLocally()
        {
            return Locales
                .Where(pl => !LocalizationSettings.AvailableLocales.Locales.Any(l => l.Identifier.Code == pl.code))
                .ToList();
        }

        public void CreatePhraseLocale(UnityEngine.Localization.Locale locale)
        {
            Client.CreateLocale(m_selectedProjectId, locale.Identifier.Code, locale.Identifier.Code);
        }

        public void PushAll()
        {
            int count = 0;
            List<StringTableCollection> collections = ConnectedStringTableCollections();
            foreach (StringTableCollection collection in collections)
            {
                // iterate the serialized properties
                var phraseExtension = collection.Extensions.FirstOrDefault(e => e is PhraseExtension) as PhraseExtension;
                if (phraseExtension != null)
                {
                    if (m_pushOnlySelected && m_selectedLocaleId != null)
                    {
                        Log("Looking for locale " + m_selectedLocaleId);
                        var selectedLocale = Locales.FirstOrDefault(l => l.code == m_selectedLocaleId);
                        if (selectedLocale != null)
                        {
                            Log("Pushing locale " + selectedLocale.code);
                            Push(collection, selectedLocale);
                            count++;
                        }
                    }
                    else
                    {
                        count += Push(collection);
                    }
                }
            }
            EditorUtility.DisplayDialog("Push complete", $"{count} locale(s) from {collections.Count} table collection(s) pushed.", "OK");
        }

        public async void PullAll()
        {
            int totalLocaleCount = 0;
            int totalCount = 0;
            foreach (StringTableCollection collection in ConnectedStringTableCollections())
            {
                totalLocaleCount += await Pull(collection);
                totalCount++;
            }
            EditorUtility.DisplayDialog("Pull complete", $"{totalLocaleCount} locale(s) in {totalCount} table collection(s) imported.", "OK");
        }

        public int Push(StringTableCollection collection, bool displayDialog = false)
        {
            int count = 0;
            foreach (var stringTable in collection.StringTables)
            {
                Locale locale = Locales.FirstOrDefault(l => l.code == stringTable.LocaleIdentifier.Code);
                if (locale != null)
                {
                    Push(collection, locale);
                    count++;
                }
            }
            if (displayDialog)
            {
                EditorUtility.DisplayDialog("Push complete", $"{count} locale(s) pushed.", "OK");
            }
            return count;
        }

        public void Push(StringTableCollection collection, Locale locale, bool displayDialog = false)
        {
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
            Client.UploadFile(xlfContent, m_selectedProjectId, locale.id, false);
            if (File.Exists(path)) File.Delete(path);
            if (displayDialog)
            {
                EditorUtility.DisplayDialog("Push complete", $"Locale {locale.code} pushed.", "OK");
            }
        }

        public async Task<int> Pull(StringTableCollection collection, bool displayDialog = false)
        {
            int count = 0;
            foreach(var stringTable in collection.StringTables) {
                // find the locale
                var selectedLocale = Locales.FirstOrDefault(l => l.code == stringTable.LocaleIdentifier.Code);
                if (selectedLocale != null) {
                    if (m_pullOnlySelected && !LocaleIdsToPull.Contains(selectedLocale.id))
                    {
                        continue;
                    }
                    Log("Downloading locale " + selectedLocale.code);
                    string content = await Client.DownloadLocale(m_selectedProjectId, selectedLocale.id);
                    using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
                    {
                        Xliff.ImportDocumentIntoTable(XliffDocument.Parse(stream), stringTable);
                        count++;
                    }
                }
                else
                {
                    Log("No Phrase locale found for string table " + stringTable.LocaleIdentifier.Code);
                }
            }
            if (displayDialog)
            {
                EditorUtility.DisplayDialog("Pull complete", $"{count} locale(s) imported.", "OK");
            }
            return count;
        }
    }

    [CustomEditor(typeof(PhraseProvider))]
    public class PhraseEditor : Editor
    {
        bool m_showTables = false;

        bool m_showConnection = false;

        bool m_showLocalesMissing = false;

        static readonly string[] m_environmentOptions = { "EU", "US", "Custom" };

        public override void OnInspectorGUI()
        {
            PhraseProvider phraseProvider = target as PhraseProvider;
            serializedObject.Update();

            m_showConnection = EditorGUILayout.BeginFoldoutHeaderGroup(m_showConnection, "Phrase Connection");

            if (m_showConnection) {
                phraseProvider.m_Environment = m_environmentOptions[EditorGUILayout.Popup("Environment", System.Array.IndexOf(m_environmentOptions, phraseProvider.m_Environment), m_environmentOptions)];
                if (phraseProvider.m_Environment == "Custom") {
                    phraseProvider.m_ApiUrl = EditorGUILayout.TextField("API URL", phraseProvider.m_ApiUrl);
                } else {
                    switch (phraseProvider.m_Environment) {
                        case "EU":
                            phraseProvider.m_ApiUrl = "https://api.phrase.com/v2/";
                            break;
                        case "US":
                            phraseProvider.m_ApiUrl = "https://api.us.app.phrase.com/v2/";
                            break;
                    }
                }

                phraseProvider.m_UseOauth = !EditorGUILayout.BeginToggleGroup("Token authentication", !phraseProvider.m_UseOauth);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiKey"));
                if (GUILayout.Button("Fetch Projects"))
                {
                    phraseProvider.FetchProjects();
                }
                EditorGUILayout.EndToggleGroup();
                serializedObject.ApplyModifiedProperties();
                phraseProvider.m_UseOauth = EditorGUILayout.BeginToggleGroup("OAuth authentication", phraseProvider.m_UseOauth);

                using (new EditorGUI.DisabledScope(phraseProvider.m_OauthInProgress))
                {
                    string buttonLabel = phraseProvider.m_OauthInProgress ? "Logging in..." : "Log in using OAuth";
                    if (GUILayout.Button(buttonLabel))
                    {
                        phraseProvider.m_OauthInProgress = true;
                        PhraseOauthAuthenticator.Authenticate(phraseProvider);
                    }
                }
                EditorGUILayout.EndToggleGroup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            string[] projectNames = phraseProvider.Projects.Select(p => p.name).ToArray();
            int selectedProjectIndex = phraseProvider.Projects.FindIndex(p => p.id == phraseProvider.m_selectedProjectId);

            int selectedProjectIndexNew = EditorGUILayout.Popup("Project", selectedProjectIndex, projectNames);
            if (selectedProjectIndexNew != selectedProjectIndex)
            {
                selectedProjectIndex = selectedProjectIndexNew;
                phraseProvider.m_selectedProjectId = phraseProvider.Projects[selectedProjectIndex].id;
                phraseProvider.FetchLocales();
            }

            if (phraseProvider.IsProjectSelected && phraseProvider.HasLocaleMismatch)
            {
                m_showLocalesMissing = EditorGUILayout.BeginFoldoutHeaderGroup(m_showLocalesMissing, "Missing locales");
                if (m_showLocalesMissing)
                {
                    if (phraseProvider.MissingLocalesLocally().Count > 0)
                    {
                        EditorGUILayout.HelpBox("The following Phrase locales are missing in the project", MessageType.Warning);
                        foreach (var locale in phraseProvider.MissingLocalesLocally())
                        {
                            EditorGUILayout.LabelField(locale.ToString());
                        }
                        if (GUILayout.Button("Create locales locally"))
                        {
                            string pathToSave = EditorUtility.OpenFolderPanel("Save new locales", "Assets", "");
                            if (string.IsNullOrEmpty(pathToSave))
                            {
                                return;
                            }
                            if (!pathToSave.StartsWith(Application.dataPath)) {
                                Debug.LogError("Path must be in the Assets folder");
                                return;
                            }
                            pathToSave = pathToSave.Substring(Application.dataPath.Length - "Assets".Length);
                            int count = 0;
                            foreach (var locale in phraseProvider.MissingLocalesLocally())
                            {
                                UnityEngine.Localization.Locale newLocale = UnityEngine.Localization.Locale.CreateLocale(locale.code);
                                AssetDatabase.CreateAsset(newLocale, $"{pathToSave}/{newLocale.ToString()}.asset");
                                count++;
                            }
                            EditorUtility.DisplayDialog("Locales created", $"{count} locale(s) created and saved to {pathToSave}.", "OK");
                        }
                    }
                    if (phraseProvider.MissingLocalesRemotely().Count > 0)
                    {
                        EditorGUILayout.HelpBox("The following locales are missing in Phrase Strings", MessageType.Warning);
                        foreach (var locale in phraseProvider.MissingLocalesRemotely())
                        {
                            EditorGUILayout.LabelField(locale.Identifier.Code);
                        }
                        if (GUILayout.Button("Create locales on Phrase"))
                        {
                            int count = 0;
                            foreach (var locale in phraseProvider.MissingLocalesRemotely())
                            {
                                phraseProvider.CreatePhraseLocale(locale);
                                count++;
                            }
                            phraseProvider.FetchLocales();
                            EditorUtility.DisplayDialog("Locales created", $"{count} locale(s) created in Phrase.", "OK");
                        }
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            m_showTables = EditorGUILayout.BeginFoldoutHeaderGroup(m_showTables, "Connected string tables");
            if (m_showTables)
            {
                List<StringTableCollection> allCollections = phraseProvider.AllStringTableCollections();
                foreach (StringTableCollection collection in allCollections)
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
            EditorGUILayout.EndFoldoutHeaderGroup();

            using (new EditorGUI.DisabledScope(selectedProjectIndex < 0))
            {
                // Push locale selection
                phraseProvider.m_pushOnlySelected = EditorGUILayout.BeginToggleGroup("Push only selected locale:", phraseProvider.m_pushOnlySelected);
                string[] availableLocaleNames = LocalizationSettings.AvailableLocales.Locales.Select(l => l.Identifier.Code).ToArray();
                int selectedLocaleIndex = LocalizationSettings.AvailableLocales.Locales.FindIndex(l => l.Identifier.Code == phraseProvider.m_selectedLocaleId);
                int selectedLocaleIndexNew = EditorGUILayout.Popup("Locale", selectedLocaleIndex, availableLocaleNames);
                if (selectedLocaleIndexNew != selectedLocaleIndex)
                {
                    selectedLocaleIndex = selectedLocaleIndexNew;
                    phraseProvider.m_selectedLocaleId = LocalizationSettings.AvailableLocales.Locales[selectedLocaleIndex].Identifier.Code;
                }
                EditorGUILayout.EndToggleGroup();

                string pushButtonLabel = phraseProvider.m_pushOnlySelected ? "Push selected" : "Push all";
                if (GUILayout.Button(pushButtonLabel))
                {
                    phraseProvider.PushAll();
                }

                phraseProvider.m_pullOnlySelected = EditorGUILayout.BeginToggleGroup("Pull only selected locales:", phraseProvider.m_pullOnlySelected);

                foreach (var locale in phraseProvider.Locales)
                {
                    bool selectedState = phraseProvider.LocaleIdsToPull.Contains(locale.id);
                    bool newSelectedState = EditorGUILayout.ToggleLeft(locale.ToString(), selectedState);
                    if (newSelectedState != selectedState)
                    {
                        if (newSelectedState)
                        {
                            phraseProvider.LocaleIdsToPull.Add(locale.id);
                        }
                        else
                        {
                            phraseProvider.LocaleIdsToPull.Remove(locale.id);
                        }
                    }
                }

                EditorGUILayout.EndToggleGroup();
                string pullButtonLabel = phraseProvider.m_pullOnlySelected ? "Pull selected" : "Pull all";
                if (GUILayout.Button(pullButtonLabel))
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
