using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Linq;

namespace Phrase
{
  [CustomEditor(typeof(PhraseKeyContext))]
  public class PhraseKeyContextEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      PhraseKeyContext context = (PhraseKeyContext)target;
      if (GUILayout.Button("Upload Screenshot"))
      {
        LocalizeStringEvent localizeStringEvent = context.GetComponent<LocalizeStringEvent>();
        if (localizeStringEvent != null)
        {
          LocalizedString stringReference = localizeStringEvent.StringReference;

          // Get the key name
          var guid = stringReference.TableReference.TableCollectionNameGuid.ToString("N");
          Debug.Log($"SharedTableData GUID: {guid}");
          var path = AssetDatabase.GUIDToAssetPath(guid);
          var sharedTableData = AssetDatabase.LoadAssetAtPath<SharedTableData>(path);
          Debug.Log(sharedTableData);
          string keyName = stringReference.TableEntryReference.ResolveKeyName(sharedTableData);

          // Get Phrase provider from the table
          var stringTableCollection = PhraseProvider.ConnectedStringTableCollections().FirstOrDefault(x => x.SharedData == sharedTableData);
          Debug.Log(stringTableCollection);

          // Make the screenshot
          string screenshotPath = "Temp/phrase_screenshot.png";
          ScreenCapture.CaptureScreenshot(screenshotPath);
          // pause for 2 seconds to allow the screenshot to be saved
          System.Threading.Thread.Sleep(2000);

          // Upload screenshot
          var provider = PhraseProvider.FindFor(stringTableCollection);
          provider.UploadScreenshot(keyName, screenshotPath);

          EditorUtility.DisplayDialog("Upload Screenshot", $"Will upload screenshot for {keyName}", "OK");
        }
      }
    }
  }
}
