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

    private PhraseMetadata Metadata => SharedTableData?.GetEntry(KeyName)?.Metadata.GetMetadata<PhraseMetadata>();

    IEnumerator UploadScreenshot(string keyName, PhraseMetadata metadata)
    {
      string screenshotPath = "Temp/phrase_screenshot.png";
      System.IO.File.Delete(screenshotPath);
      EditorApplication.ExecuteMenuItem("Window/General/Game"); // screenshot only works in game view
      ScreenCapture.CaptureScreenshot(screenshotPath);

      yield return new WaitForEndOfFrame();
      Provider.UploadScreenshot(keyName, screenshotPath, metadata);
      System.IO.File.Delete(screenshotPath);

      EditorUtility.DisplayDialog("Upload Screenshot", $"Screenshot uploaded for {KeyName}", "OK");
    }

    public override void OnInspectorGUI()
    {
      bool isConnected = SharedTableData != null && Provider != null && KeyName != null;
      if (isConnected)
      {
        if (Metadata == null)
        {
          SharedTableData.GetEntry(KeyName).Metadata.AddMetadata(new PhraseMetadata());
        }
        EditorGUILayout.LabelField("Phrase Key", KeyName);
        if (Metadata.KeyId != null)
        {
          if (EditorGUILayout.LinkButton("Open in Phrase")) {
            Application.OpenURL(Provider.KeyUrl(Metadata.KeyId));
          }
        }
        Metadata.Description = EditorGUILayout.TextField("Description", Metadata.Description);
        Metadata.MaxLength = EditorGUILayout.IntField("Max Length (0 for no limit)", Metadata.MaxLength);
        if (GUILayout.Button("Upload Screenshot"))
        {
          EditorCoroutineUtility.StartCoroutine(UploadScreenshot(KeyName, Metadata), this);
        }
      }
      else
      {
        EditorGUILayout.LabelField("This object is not connected to a Phrase key.");
      }
    }
  }
}
