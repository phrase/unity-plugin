using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Linq;
using UnityEditor.Localization;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace Phrase
{
  [CustomEditor(typeof(PhraseKeyContext))]
  public class PhraseKeyContextEditor : Editor
  {
    private PhraseKeyContext Context => (PhraseKeyContext)target;

    private LocalizeStringEvent LocalizeStringEvent => Context?.GetComponent<LocalizeStringEvent>();

    private LocalizedString StringReference => LocalizeStringEvent?.StringReference;

    private SharedTableData SharedTableData
    {
      get
      {
        if (LocalizeStringEvent == null)
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
      bool isConnected = LocalizeStringEvent != null && Provider != null && KeyName != null;
      if (isConnected)
      {
        EditorGUILayout.LabelField("Key Name", KeyName);
        EditorGUILayout.LabelField("Description", Context.Description);
        EditorGUILayout.LabelField("Screenshot ID", Context.ScreenshotId);
        if (GUILayout.Button("Upload Screenshot"))
        {
          if (LocalizeStringEvent != null)
          {
            EditorCoroutineUtility.StartCoroutine(UploadScreenshot(KeyName, Context), this);
          }
        }
      }
      else
      {
        EditorGUILayout.LabelField("This object is not connected to a Phrase key.");
      }
    }
  }
}
