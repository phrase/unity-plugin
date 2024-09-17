using System.Collections;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.Tables;

namespace Phrase
{
  class PhraseEditor : EditorWindow
  {
    [MenuItem("Window/Phrase")]
    public static void ShowWindow()
    {
      var window = GetWindow<PhraseEditor>("Phrase");
      window.Show();
    }

    private LocalizedString LocalizedString(Transform gameObject)
    {
      var localizeStringEvent = gameObject.GetComponent<LocalizeStringEvent>();
      if (localizeStringEvent != null)
      {
        return localizeStringEvent.StringReference;
      }

      var gameObjectLocalizer = gameObject.GetComponentInParent<GameObjectLocalizer>();
      if (gameObjectLocalizer != null)
      {
        var trackedObject = gameObjectLocalizer.TrackedObjects[0];
        if (trackedObject != null)
        {
          var trackedProperty = trackedObject.TrackedProperties[0];
          if (trackedProperty != null)
          {
            return (trackedProperty as LocalizedStringProperty).LocalizedString;
          }
        }
      }

      return null;
    }

    private PhraseProvider Provider(SharedTableData sharedTableData)
    {
      var stringTableCollection = PhraseProvider.ConnectedStringTableCollections().FirstOrDefault(x => x.SharedData == sharedTableData);
      return PhraseProvider.FindFor(stringTableCollection);
    }

    private SharedTableData SharedTableData(LocalizedString localizedString)
    {
      if (localizedString == null)
      {
        return null;
      }
      var guid = localizedString.TableReference.TableCollectionNameGuid.ToString("N");
      var path = AssetDatabase.GUIDToAssetPath(guid);
      return AssetDatabase.LoadAssetAtPath<SharedTableData>(path);
    }

    private SharedTableData SharedTableData(Transform gameObject)
    {
      var localizedString = LocalizedString(gameObject);
      return SharedTableData(localizedString);
    }

    private string KeyName(LocalizedString localizedString, SharedTableData sharedTableData = null)
    {
      if (sharedTableData == null) sharedTableData = SharedTableData(localizedString);
      if (localizedString != null && sharedTableData != null)
      {
        return localizedString.TableEntryReference.ResolveKeyName(sharedTableData);
      }

      return null;
    }

    private PhraseMetadata PhraseMetadata(SharedTableData sharedTableData, string keyName)
    {
      if (sharedTableData != null)
      {
        return sharedTableData.GetEntry(keyName).Metadata.GetMetadata<PhraseMetadata>();
      }

      return null;
    }

    private IEnumerator UploadScreenshots(Transform[] gameObjects)
    {
      string screenshotPath = "Temp/phrase_screenshot.png";
      System.IO.File.Delete(screenshotPath);
      EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
      ScreenCapture.CaptureScreenshot(screenshotPath);

      yield return new WaitForEndOfFrame();

      var groupedObjectsByProvider = gameObjects.GroupBy(x => {
        SharedTableData sharedTableData = SharedTableData(x);
        PhraseProvider provider = Provider(sharedTableData);
        return provider;
      }).ToDictionary(g => g.Key, g => g.Select(x => {
        var localizedString = LocalizedString(x);
        var sharedTableData = SharedTableData(localizedString);
        var keyName = KeyName(localizedString, sharedTableData);
        return PhraseMetadata(sharedTableData, keyName);
      }).ToList());

      foreach (var group in groupedObjectsByProvider)
      {
        PhraseProvider provider = group.Key;
        provider.UploadScreenshot(group.Value, screenshotPath);
      }
      System.IO.File.Delete(screenshotPath);

      EditorUtility.DisplayDialog($"Upload Screenshot", $"Screenshot uploaded for {gameObjects.Length} key(s)", "OK");
    }

    private Vector2 scrollPosition;

    private Transform[] translatableObjects;

    public void OnGUI()
    {
      var hasScreenshots = false;

      if (translatableObjects == null || translatableObjects.Length == 0)
      {
        EditorGUILayout.HelpBox("Select a localized GameObject to edit its Phrase metadata.", MessageType.Info);
        return;
      }
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
      foreach (var gameObject in translatableObjects)
      {
        LocalizedString localizedString = LocalizedString(gameObject);
        SharedTableData sharedTableData = SharedTableData(localizedString);
        PhraseProvider provider = Provider(sharedTableData);
        string keyName = KeyName(localizedString, sharedTableData);
        PhraseMetadata metadata = PhraseMetadata(sharedTableData, keyName);
        if (metadata == null)
        {
          metadata = new PhraseMetadata();
          sharedTableData.GetEntry(keyName).Metadata.AddMetadata(metadata);
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(gameObject.name, EditorStyles.boldLabel);
        if (metadata.KeyId != null)
        {
          if (GUILayout.Button("Open in Phrase", GUILayout.Width(100)))
          {
            Application.OpenURL(provider.KeyUrl(metadata.KeyId));
          }
        }
        if (!string.IsNullOrEmpty(metadata.ScreenshotUrl))
        {
          if (GUILayout.Button("Open Screenshot"))
          {
            Application.OpenURL(metadata.ScreenshotUrl);
          }
        }
        EditorGUILayout.EndHorizontal();
        if (keyName != null)
        {
          EditorGUI.indentLevel++;
          EditorGUILayout.BeginHorizontal();
          EditorGUILayout.LabelField("Phrase Key", keyName);
          if (metadata.KeyId != null)
          {
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
              EditorGUIUtility.systemCopyBuffer = keyName;
            }
            if (metadata.ScreenshotId != null)
            {
              hasScreenshots = true;
            }
          }
          EditorGUILayout.EndHorizontal();
          metadata.Description = EditorGUILayout.TextField("Description", metadata.Description);
          metadata.MaxLength = EditorGUILayout.IntField(new GUIContent("Max Length", "set 0 for no limit"), metadata.MaxLength);
          EditorGUI.indentLevel--;
        }
      }

      GUILayout.Space(20);

      var screenshotButtonLabel = hasScreenshots ? "Update Screenshot" : "Upload Screenshot";
      if (GUILayout.Button(screenshotButtonLabel))
      {
        EditorCoroutineUtility.StartCoroutine(UploadScreenshots(translatableObjects), this);
      }

      EditorGUILayout.EndScrollView();
    }

    public void OnSelectionChange()
    {
      // This finds all selected GameObjects and their children that have a LocalizedString component
      // TODO: check how it behaves with lots of objects
      translatableObjects = Selection.transforms
        ?.SelectMany(x => x.GetComponentsInChildren<Transform>())
        ?.Where(x => {
          var localizedString = LocalizedString(x);
          if (localizedString == null) return false;
          var sharedTableData = SharedTableData(localizedString);
          var provider = Provider(sharedTableData);
          return provider != null;
        })
        ?.ToArray();
      Repaint();
    }
  }
}
