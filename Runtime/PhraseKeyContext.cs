using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;

namespace Phrase
{
  [ExecuteInEditMode]
  [AddComponentMenu("Localization/Phrase Key Context")]
  public class PhraseKeyContext : MonoBehaviour
  {
    public string Description = "";
  }
}
