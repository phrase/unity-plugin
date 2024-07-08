using UnityEngine;
using UnityEditor.Localization;

namespace Phrase
{
    [StringTableCollectionExtension]
    public class PhraseExtension : CollectionExtension
    {
        public PhraseProvider m_provider;
    }
}