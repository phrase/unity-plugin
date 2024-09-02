using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Linq;
using UnityEditor.Localization;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;

namespace Phrase
{
  [CustomEditor(typeof(PhraseKeyContext))]
  public class PhraseKeyContextEditor : Editor
  {
    private PhraseKeyContext Context => (PhraseKeyContext)target;

    private LocalizeStringEvent LocalizeStringEvent => Context?.GetComponent<LocalizeStringEvent>();

    private LocalizedString StringReference
    {
      get
      {
        if (LocalizeStringEvent != null)
        {
          return LocalizeStringEvent.StringReference;
        }

        GameObjectLocalizer localizer = Context.GetComponentInParent<GameObjectLocalizer>();
        if (localizer != null)
        {
          var trackedObject = localizer.TrackedObjects.FirstOrDefault();
          if (trackedObject != null)
          {
            var trackedProperty = trackedObject.TrackedProperties.FirstOrDefault();
            if (trackedProperty != null)
            {
              return (trackedProperty as LocalizedStringProperty).LocalizedString;
            }
          }
        }

        return null;
      }
    }

    private SharedTableData SharedTableData
    {
      get
      {
        if (StringReference == null)
        {
          return null;
        }

        var guid = StringReference.TableReference.TableCollectionNameGuid.ToString("N");
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return AssetDatabase.LoadAssetAtPath<SharedTableData>(path);
      }
    }

    private string KeyName => StringReference.TableEntryReference.ResolveKeyName(SharedTableData);

    private StringTableCollection StringTableCollection => PhraseProvider.ConnectedStringTableCollections().FirstOrDefault(x => x.SharedData == SharedTableData);

    private PhraseProvider Provider => PhraseProvider.FindFor(StringTableCollection);

    IEnumerator UploadScreenshot(string keyName, PhraseKeyContext context)
    {
      string screenshotPath = "Temp/phrase_screenshot.png";
      System.IO.File.Delete(screenshotPath);
      EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
      ScreenCapture.CaptureScreenshot(screenshotPath);

      yield return new WaitForEndOfFrame();
      Provider.UploadScreenshot(keyName, screenshotPath, context);
      System.IO.File.Delete(screenshotPath);

      EditorUtility.DisplayDialog("Upload Screenshot", $"Screenshot uploaded for {KeyName}", "OK");
    }

    public override void OnInspectorGUI()
    {
      bool isConnected = SharedTableData != null && Provider != null && KeyName != null;
      if (isConnected)
      {
        EditorGUILayout.LabelField("Key Name", KeyName);
        EditorGUILayout.LabelField("Description", Context.Description);
        EditorGUILayout.LabelField("Screenshot ID", Context.ScreenshotId);
        if (GUILayout.Button("Upload Screenshot"))
        {
          EditorCoroutineUtility.StartCoroutine(UploadScreenshot(KeyName, Context), this);
        }
      }
      else
      {
        EditorGUILayout.LabelField("This object is not connected to a Phrase key.");
      }
    }
  }
}
