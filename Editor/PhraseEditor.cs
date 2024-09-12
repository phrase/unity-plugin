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

    private PhraseProvider Provider(GameObject gameObject)
    {
      var sharedTableData = SharedTableData(gameObject);
      var stringTableCollection = PhraseProvider.ConnectedStringTableCollections().FirstOrDefault(x => x.SharedData == sharedTableData);
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

    private IEnumerator UploadScreenshot(string keyName, PhraseMetadata metadata, PhraseProvider provider)
    {
      string screenshotPath = "Temp/phrase_screenshot.png";
      System.IO.File.Delete(screenshotPath);
      EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
      ScreenCapture.CaptureScreenshot(screenshotPath);

      yield return new WaitForEndOfFrame();
      provider.UploadScreenshot(keyName, screenshotPath, metadata);
      System.IO.File.Delete(screenshotPath);

      EditorUtility.DisplayDialog("Upload Screenshot", $"Screenshot uploaded for key \"{keyName}\"", "OK");
    }

    private Vector2 scrollPosition;

    public void OnGUI()
    {
      // This finds all selected GameObjects and their children that have a LocalizedString component
      // TODO: check how it behaves with lots of objects
      var translatableObjects = Selection.gameObjects
        .SelectMany(x => x.GetComponentsInChildren<Transform>())
        .Select(x => x.gameObject)
        .Where(x => LocalizedString(x) != null && Provider(x) != null)
        .ToArray();
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
          PhraseProvider provider = Provider(gameObject);
          PhraseMetadata metadata = phraseMetadata(gameObject);
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
          // TODO: extract screenshot upload to apply to multiple keys
          if (GUILayout.Button("Upload Screenshot"))
          {
            EditorCoroutineUtility.StartCoroutine(UploadScreenshot(keyName, metadata, provider), this);
          }
          EditorGUI.indentLevel--;
        }
      }
      EditorGUILayout.EndScrollView();
    }

    public void OnSelectionChange()
    {
      Repaint();
    }
  }
}
