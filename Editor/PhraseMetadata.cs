using System;
using UnityEngine.Localization.Metadata;

namespace Phrase
{
  [Serializable]
  [Metadata(AllowedTypes = MetadataType.SharedStringTableEntry)]
  public class PhraseMetadata : IMetadata
  {
    public string Description;
    public int MaxLength;
  }
}
