using CsvHelper;

using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine.Localization.Tables;

namespace Phrase
{
    public class PhraseCsvColumns : CsvColumns
    {
        int m_DescriptionIndex, m_MaxCharsIndex;

        private const string k_Description = "comment";
        private const string k_MaxChars = "max_characters_allowed";

        public override void ReadBegin(StringTableCollection collection, CsvReader reader)
        {
            m_DescriptionIndex = reader.GetFieldIndex(k_Description, isTryGet: true);
            m_MaxCharsIndex = reader.GetFieldIndex(k_MaxChars, isTryGet: true);
        }

        public override void ReadRow(SharedTableData.SharedTableEntry keyEntry, CsvReader reader)
        {
            // Get the metadata or add one
            var metadata = keyEntry.Metadata.GetMetadata<PhraseMetadata>();
            if (metadata == null)
            {
                metadata = new PhraseMetadata();
                keyEntry.Metadata.AddMetadata(metadata);
            }

            if (m_DescriptionIndex != -1)
            {
                metadata.Description = reader.GetField(m_DescriptionIndex);
            }

            if (m_MaxCharsIndex != -1)
            {
                if (reader.TryGetField<int>(m_MaxCharsIndex, out var maxChars))
                {
                    metadata.MaxLength = maxChars;
                }
                else
                {
                    metadata.MaxLength = 0;
                }
            }
        }

        public override void WriteBegin(StringTableCollection collection, CsvWriter writer)
        {
            writer.WriteField(k_Description);
            writer.WriteField(k_MaxChars);
        }

        public override void WriteRow(SharedTableData.SharedTableEntry keyEntry, IList<StringTableEntry> tableEntries, CsvWriter writer)
        {
            var metadata = keyEntry.Metadata.GetMetadata<PhraseMetadata>();
            if (metadata != null)
            {
                writer.WriteField(metadata.Description, true);
                if (metadata.MaxLength > 0)
                {
                    writer.WriteField(metadata.MaxLength);
                }
                else
                {
                    writer.WriteField(string.Empty);
                }
                return;
            }

            // Write empty entries
            writer.WriteField(string.Empty);
            writer.WriteField(string.Empty);
        }
    }
}
