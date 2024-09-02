using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;

namespace Phrase
{
  [ExecuteInEditMode]
  [RequireComponent(typeof(LocalizeStringEvent))]
  [AddComponentMenu("Localization/Phrase Key Context")]
  public class PhraseKeyContext : MonoBehaviour
  {
    [Tooltip("A description of the context in which this key is used.")]
    public string Description = "";

    [Tooltip("The screenshot id to use for this key.")]
    public string ScreenshotId = "";
  }
}
