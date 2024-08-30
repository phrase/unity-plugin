using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Linq;
using UnityEditor.Localization;
using static Phrase.PhraseClient;

namespace Phrase
{
  [CustomEditor(typeof(PhraseKeyContext))]
  public class PhraseKeyContextEditor : Editor
  {

    private bool isWritingScreenshot = false;
    private string screenshotPath = "";

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
    public override void OnInspectorGUI()
    {
      bool isConnected = LocalizeStringEvent != null && Provider != null && KeyName != null;
      if (isConnected)
      {
        EditorGUILayout.LabelField("Key Name", KeyName);
        EditorGUILayout.LabelField("Description", Context.Description);
        EditorGUILayout.LabelField("Screenshot ID", Context.ScreenshotId);
        if (isWritingScreenshot) {
          EditorGUILayout.LabelField("Uploading screenshot...");
          if (System.IO.File.Exists(screenshotPath)) {
            Screenshot screenshot = Provider.UploadScreenshot(KeyName, screenshotPath).Result;
            System.IO.File.Delete(screenshotPath);
            Context.ScreenshotId = screenshot.id;

            EditorUtility.DisplayDialog("Upload Screenshot", $"Screenshot uploaded for {KeyName}", "OK");
            isWritingScreenshot = false;
          }
        }
        else {
          if (GUILayout.Button("Upload Screenshot"))
          {
            if (LocalizeStringEvent != null)
            {
              screenshotPath = "Temp/phrase_screenshot.png";
              System.IO.File.Delete(screenshotPath);
              EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
              ScreenCapture.CaptureScreenshot(screenshotPath);
              // the screenshot is only written in the next frame
              isWritingScreenshot = true;
            }
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
