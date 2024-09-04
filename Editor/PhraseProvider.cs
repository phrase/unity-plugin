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
            // Initialize the Projects list to accumulate all fetched projects
            Projects = new List<Project>();

            int page = 1;
            List<Project> currentBatch;

            do
            {
                currentBatch = await Client.ListProjects(page);
                Projects.AddRange(currentBatch);
                page++;
            } while (currentBatch.Count == 100);  // Continue if a full batch is returned

            FetchLocales();
        }

        public async void FetchLocales()
        {
            if (m_selectedProjectId == null || m_selectedProjectId == "")
            {
                return;
            }
            Locales = await Client.ListLocales(m_selectedProjectId);
            LocalizationSettings.InitializationOperation.WaitForCompletion();
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

        /// <summary>
        /// Returns the StringTableCollections that are connected to Phrase provider
        /// </summary>
        /// <returns></returns>
        public List<StringTableCollection> ConnectedStringTableCollections()
        {
            return AllStringTableCollections()
                .Where(collection => collection.Extensions.Any(e => e is PhraseExtension))
                .ToList();
        }

        /// <summary>
        /// Returns the Unity locales that are missing in Phrase
        /// </summary>
        /// <returns></returns>
        public List<UnityEngine.Localization.Locale> MissingLocalesRemotely()
        {
            return LocalizationSettings.AvailableLocales.Locales
                .Where(l => !Locales.Any(pl => pl.code == l.Identifier.Code))
                .ToList();
        }

        /// <summary>
        /// Returns the Unity locales that are existing in Phrase
        /// </summary>
        /// <returns></returns>
        public List<UnityEngine.Localization.Locale> AvailableLocalesRemotely()
        {
            return LocalizationSettings.AvailableLocales.Locales
                .Where(l => Locales.Any(pl => pl.code == l.Identifier.Code))
                .ToList();
        }

        /// <summary>
        /// Returns the Phrase locales that are missing in the Unity project
        /// </summary>
        /// <returns></returns>
        public List<Locale> MissingLocalesLocally()
        {
            return Locales
                .Where(pl => !LocalizationSettings.AvailableLocales.Locales.Any(l => l.Identifier.Code == pl.code))
                .ToList();
        }

        /// <summary>
        /// Returns the Phrase locales that are existing in the Unity project
        /// </summary>
        /// <returns></returns>
        public List<Locale> AvailableLocalesLocally()
        {
            return Locales
                .Where(pl => LocalizationSettings.AvailableLocales.Locales.Any(l => l.Identifier.Code == pl.code))
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
            Client.UploadFile(path, m_selectedProjectId, locale.id, false);
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

        bool selectAllLocalesToCreateLocally = false;
        bool selectAllLocalesToCreateRemotely = false;
        List<string> selectedLocalesToCreateLocally = new List<string>();
        List<string> selectedLocalesToCreateRemotely = new List<string>();

        private PhraseProvider phraseProvider => target as PhraseProvider;

        private string searchQuery = "";
        private string localeSearchQuery = string.Empty;
    
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ShowConnectionSection();
            ShowProjectSection();
            ShowLocaleMismatchSection();
            ShowConnectedTablesSection();
            ShowPushPullSection();

            serializedObject.ApplyModifiedProperties();
        }

        private string selectedProjectName;

        private string TruncateWithEllipsis(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            {
                return input;
            }
            return input.Substring(0, maxLength) + "...";
        }

        private void ShowConnectionSection() {
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
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApiKey"));
                if (GUILayout.Button("Fetch Projects"))
                {
                    phraseProvider.FetchProjects();
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndToggleGroup();
                serializedObject.ApplyModifiedProperties();
                phraseProvider.m_UseOauth = EditorGUILayout.BeginToggleGroup("OAuth authentication", phraseProvider.m_UseOauth);
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(phraseProvider.m_OauthInProgress))
                {
                    string buttonLabel = phraseProvider.m_OauthInProgress ? "Logging in..." : "Log in using OAuth";
                    if (GUILayout.Button(buttonLabel))
                    {
                        PhraseOauthAuthenticator.Authenticate(phraseProvider);
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndToggleGroup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ShowProjectSection() {
            var allProjects = phraseProvider.Projects;
            var filteredProjects = allProjects.ToArray();

            // Only show Project-Filter if there are more than 10 projects
            if (allProjects.Count > 10)
            {
                EditorGUILayout.BeginHorizontal();
                searchQuery = EditorGUILayout.TextField("Filter by Projectname", searchQuery);
                // Filter projects based on the search query
                filteredProjects = allProjects
                    .Where(p => string.IsNullOrEmpty(searchQuery) || p.name.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
                EditorGUILayout.LabelField($"Projects: {filteredProjects.Length} / {phraseProvider.Projects.Count}", GUILayout.MaxWidth(120));
                EditorGUILayout.EndHorizontal();
            }

            // Truncate project names more aggressively to control popup width
            string[] projectNames = filteredProjects
                .Select(p => TruncateWithEllipsis(p.name, 30))
                .ToArray();

            int selectedProjectIndex = System.Array.IndexOf(filteredProjects, phraseProvider.Projects.FirstOrDefault(p => p.id == phraseProvider.m_selectedProjectId));

            // Ensure valid index
            if (selectedProjectIndex == -1 && filteredProjects.Length > 0)
            {
                selectedProjectIndex = 0; // Default to first project if no selection matches
            }

            selectedProjectIndex = EditorGUILayout.Popup("Select Project", selectedProjectIndex, projectNames);

            if (selectedProjectIndex >= 0 && selectedProjectIndex < filteredProjects.Length)
            {
                var selectedProject = filteredProjects[selectedProjectIndex];
                if (selectedProjectName != selectedProject.name)
                {
                    selectedProjectName = selectedProject.name;
                    phraseProvider.m_selectedProjectId = selectedProject.id;
                    phraseProvider.FetchLocales();
                }
            }
        }

        private void ShowLocaleMismatchSection() {
            if (phraseProvider.IsProjectSelected && phraseProvider.HasLocaleMismatch)
            {
                m_showLocalesMissing = EditorGUILayout.BeginFoldoutHeaderGroup(m_showLocalesMissing, "Missing locales");
                if (m_showLocalesMissing)
                {
                    ShowLocalLocaleMissingSection();

                    ShowRemoteLocaleMissingSection();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        private void ShowLocalLocaleMissingSection() {
            if (phraseProvider.MissingLocalesLocally().Count > 0)
            {
                EditorGUILayout.HelpBox("The following Phrase locales are missing in the project:", MessageType.None);
                if (EditorGUILayout.ToggleLeft("Select all", selectAllLocalesToCreateLocally, EditorStyles.boldLabel))
                {
                    if (!selectAllLocalesToCreateLocally)
                    {
                        selectedLocalesToCreateLocally = phraseProvider.MissingLocalesLocally().Select(l => l.code).ToList();
                        selectAllLocalesToCreateLocally = true;
                    }
                }
                else
                {
                    if (selectAllLocalesToCreateLocally)
                    {
                        selectedLocalesToCreateLocally.Clear();
                        selectAllLocalesToCreateLocally = false;
                    }
                }
                EditorGUI.indentLevel++;
                foreach (var locale in phraseProvider.MissingLocalesLocally())
                {
                    if (EditorGUILayout.ToggleLeft(locale.ToString(), selectedLocalesToCreateLocally.Contains(locale.code)))
                    {
                        if (!selectedLocalesToCreateLocally.Contains(locale.code))
                        {
                            selectedLocalesToCreateLocally.Add(locale.code);
                        }
                    }
                    else
                    {
                        selectedLocalesToCreateLocally.Remove(locale.code);
                    }
                }
                EditorGUI.indentLevel--;

                using (new EditorGUI.DisabledScope(selectedLocalesToCreateLocally.Count == 0))
                {
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
                            if (!selectedLocalesToCreateLocally.Contains(locale.code))
                            {
                                continue;
                            }
                            UnityEngine.Localization.Locale newLocale = UnityEngine.Localization.Locale.CreateLocale(locale.code);
                            AssetDatabase.CreateAsset(newLocale, $"{pathToSave}/{newLocale.ToString()}.asset");
                            count++;
                        }
                        LocalizationSettings.InitializationOperation.WaitForCompletion();
                        EditorUtility.DisplayDialog("Locales created", $"{count} locale(s) created and saved to {pathToSave}.", "OK");
                    }
                }
            }
        }

        private void ShowRemoteLocaleMissingSection() {
            if (phraseProvider.MissingLocalesRemotely().Count > 0)
            {
                EditorGUILayout.HelpBox("The following locales are missing in Phrase Strings:", MessageType.None);
                if (EditorGUILayout.ToggleLeft("Select all", selectAllLocalesToCreateRemotely, EditorStyles.boldLabel))
                {
                    if (!selectAllLocalesToCreateRemotely)
                    {
                        selectedLocalesToCreateRemotely = phraseProvider.MissingLocalesRemotely().Select(l => l.Identifier.Code).ToList();
                        selectAllLocalesToCreateRemotely = true;
                    }
                }
                else
                {
                    if (selectAllLocalesToCreateRemotely)
                    {
                        selectedLocalesToCreateRemotely.Clear();
                        selectAllLocalesToCreateRemotely = false;
                    }
                }
                EditorGUI.indentLevel++;
                foreach (var locale in phraseProvider.MissingLocalesRemotely())
                {
                    if (EditorGUILayout.ToggleLeft(locale.ToString(), selectedLocalesToCreateRemotely.Contains(locale.Identifier.Code)))
                    {
                        if (!selectedLocalesToCreateRemotely.Contains(locale.Identifier.Code))
                        {
                            selectedLocalesToCreateRemotely.Add(locale.Identifier.Code);
                        }
                    }
                    else
                    {
                        selectedLocalesToCreateRemotely.Remove(locale.Identifier.Code);
                    }
                }
                EditorGUI.indentLevel--;
                using (new EditorGUI.DisabledScope(selectedLocalesToCreateRemotely.Count == 0))
                {
                    if (GUILayout.Button("Create locales on Phrase"))
                    {
                        int count = 0;
                        foreach (var locale in phraseProvider.MissingLocalesRemotely())
                        {
                            if (!selectedLocalesToCreateRemotely.Contains(locale.Identifier.Code))
                            {
                                continue;
                            }
                            phraseProvider.CreatePhraseLocale(locale);
                            count++;
                        }
                        phraseProvider.FetchLocales();
                        EditorUtility.DisplayDialog("Locales created", $"{count} locale(s) created in Phrase.", "OK");
                    }
                }
            }

        }

        private void ShowConnectedTablesSection() {
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
        }

        private void ShowPushPullSection() {
            using (new EditorGUI.DisabledScope(!phraseProvider.IsProjectSelected))
            {
               ShowPushSection();
               ShowPullSection();
            }
        }

        private void ShowPushSection() {
            phraseProvider.m_pushOnlySelected = EditorGUILayout.BeginToggleGroup("Push only selected locale:", phraseProvider.m_pushOnlySelected);
            EditorGUI.indentLevel++;
            // Get the list of available locales
            var allLocales = phraseProvider.AvailableLocalesRemotely();
            
            // Determine if the search field should be displayed
            if (allLocales.Count > 10)
            {
                localeSearchQuery = EditorGUILayout.TextField("Filter by Locale", localeSearchQuery);
            }
            else
            {
                localeSearchQuery = string.Empty;
            }

            var filteredLocales = allLocales
                .Where(l => string.IsNullOrEmpty(localeSearchQuery) || l.Identifier.Code.IndexOf(localeSearchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            string[] availableLocaleNames = filteredLocales.Select(l => l.Identifier.Code).ToArray();
            int selectedLocaleIndex = System.Array.IndexOf(availableLocaleNames, phraseProvider.m_selectedLocaleId);

            // Ensure valid index
            if (selectedLocaleIndex == -1 && availableLocaleNames.Length > 0)
            {
                selectedLocaleIndex = 0; 
            }

            selectedLocaleIndex = EditorGUILayout.Popup("Locale", selectedLocaleIndex, availableLocaleNames);

            if (selectedLocaleIndex >= 0 && selectedLocaleIndex < availableLocaleNames.Length)
            {
                phraseProvider.m_selectedLocaleId = availableLocaleNames[selectedLocaleIndex];
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndToggleGroup();

            string pushButtonLabel = phraseProvider.m_pushOnlySelected ? "Push selected" : "Push all";
            if (GUILayout.Button(pushButtonLabel))
            {
                phraseProvider.PushAll();
            }
        }

        private void ShowPullSection() {
            phraseProvider.m_pullOnlySelected = EditorGUILayout.BeginToggleGroup("Pull only selected locales:", phraseProvider.m_pullOnlySelected);
            EditorGUI.indentLevel++;
            foreach (var locale in phraseProvider.AvailableLocalesLocally())
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
            EditorGUI.indentLevel--;

            EditorGUILayout.EndToggleGroup();
            string pullButtonLabel = phraseProvider.m_pullOnlySelected ? "Pull selected" : "Pull all";
            if (GUILayout.Button(pullButtonLabel))
            {
                phraseProvider.PullAll();
            }
        }

        /// <summary>
        /// Attaches or detaches the PhraseExtension to the StringTableCollection
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="selectedState"></param>
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
