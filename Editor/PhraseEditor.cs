using System.Collections;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Localization;
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

    private LocalizedString LocalizedString(GameObject gameObject)
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

    private StringTableCollection stringTableCollection(SharedTableData sharedTableData)
    {
      return PhraseProvider.ConnectedStringTableCollections().FirstOrDefault(x => x.SharedData == sharedTableData);
    }

    private PhraseProvider Provider(StringTableCollection stringTableCollection)
    {
      return PhraseProvider.FindFor(stringTableCollection);
    }

    private SharedTableData SharedTableData(GameObject gameObject)
    {
      var localizedString = LocalizedString(gameObject);
      if (localizedString != null)
      {
        var guid = localizedString.TableReference.TableCollectionNameGuid.ToString("N");
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return AssetDatabase.LoadAssetAtPath<SharedTableData>(path);
      }

      return null;
    }

    private string KeyName(GameObject gameObject)
    {
      var localizedString = LocalizedString(gameObject);
      var sharedTableData = SharedTableData(gameObject);
      if (localizedString != null && sharedTableData != null)
      {
        return localizedString.TableEntryReference.ResolveKeyName(sharedTableData);
      }

      return null;
    }

    private PhraseMetadata phraseMetadata(GameObject gameObject)
    {
      var sharedTableData = SharedTableData(gameObject);
      if (sharedTableData != null)
      {
        var keyName = KeyName(gameObject);
        if (keyName != null)
        {
          return sharedTableData.GetEntry(keyName).Metadata.GetMetadata<PhraseMetadata>();
        }
      }

      return null;
    }

    private IEnumerator UploadScreenshots(GameObject[] gameObjects)
    {
      string screenshotPath = "Temp/phrase_screenshot.png";
      System.IO.File.Delete(screenshotPath);
      EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
      ScreenCapture.CaptureScreenshot(screenshotPath);

      yield return new WaitForEndOfFrame();

      var groupedObjectsByProvider = gameObjects.GroupBy(x => {
        SharedTableData sharedTableData = SharedTableData(x);
        PhraseProvider provider = Provider(stringTableCollection(sharedTableData));
        return provider;
      }).ToDictionary(g => g.Key, g => g.Select(x => phraseMetadata(x)).ToList());

      foreach (var group in groupedObjectsByProvider) 
      {
        PhraseProvider provider = group.Key;
        provider.UploadScreenshot(group.Value, screenshotPath);
      }
      System.IO.File.Delete(screenshotPath);

      // EditorUtility.DisplayDialog("Upload Screenshot", $"Screenshot uploaded for key \"{keyName}\"", "OK");
    }

    private Vector2 scrollPosition;

    public void OnGUI()
    {
      var translatableObjects = Selection.gameObjects.Where(x => LocalizedString(x) != null).ToArray();
      if (translatableObjects.Length == 0)
      {
        EditorGUILayout.HelpBox("Select a localized GameObject to edit its Phrase metadata.", MessageType.Info);
        return;
      }
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
      foreach (var gameObject in translatableObjects)
      {
        EditorGUILayout.LabelField(gameObject.name, EditorStyles.boldLabel);
        string keyName = KeyName(gameObject);
        if (keyName != null)
        {
          EditorGUI.indentLevel++;
          SharedTableData sharedTableData = SharedTableData(gameObject);
          PhraseMetadata metadata = phraseMetadata(gameObject);
          PhraseProvider provider = Provider(stringTableCollection(sharedTableData));
          if (metadata == null)
          {
            metadata = new PhraseMetadata();
            sharedTableData.GetEntry(keyName).Metadata.AddMetadata(metadata);
          }
          EditorGUILayout.BeginHorizontal();
          EditorGUILayout.LabelField("Phrase Key", keyName);
          if (metadata.KeyId != null)
          {
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
              EditorGUIUtility.systemCopyBuffer = keyName;
            }
            if (GUILayout.Button("Open in Phrase", GUILayout.Width(100))) {
              Application.OpenURL(provider.KeyUrl(metadata.KeyId));
            }
          }
          EditorGUILayout.EndHorizontal();
          metadata.Description = EditorGUILayout.TextField("Description", metadata.Description);
          metadata.MaxLength = EditorGUILayout.IntField(new GUIContent("Max Length", "set 0 for no limit"), metadata.MaxLength);
          
          if(metadata.ScreenshotId != null)
          { 
            EditorGUI.indentLevel++;
            if (GUILayout.Button("Open Screenshot")) {
              Application.OpenURL(metadata.ScreenshotUrl);
            }
          }

          EditorGUI.indentLevel--;
        }
      }

      GUILayout.Space(20);

      if (GUILayout.Button("Upload Screenshot", GUILayout.Height(50)))
      {
        EditorCoroutineUtility.StartCoroutine(UploadScreenshots(translatableObjects), this);
      }

      EditorGUILayout.EndScrollView();
    }

    public void OnSelectionChange()
    {
      Repaint();
    }
  }
}
