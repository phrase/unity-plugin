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

        public override void ReadBegin(StringTableCollection collection, CsvReader reader)
        {
            m_DescriptionIndex = reader.GetFieldIndex("Description", isTryGet: true);
            m_MaxCharsIndex = reader.GetFieldIndex("Max Chars", isTryGet: true);
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
                metadata.MaxLength = reader.GetField<int>(m_MaxCharsIndex);
            }
        }

        public override void WriteBegin(StringTableCollection collection, CsvWriter writer)
        {
            writer.WriteField("Description");
            writer.WriteField("Max Chars");
        }

        public override void WriteRow(SharedTableData.SharedTableEntry keyEntry, IList<StringTableEntry> tableEntries, CsvWriter writer)
        {
            var metadata = keyEntry.Metadata.GetMetadata<PhraseMetadata>();
            if (metadata != null)
            {
                writer.WriteField(metadata.Description, true);
                writer.WriteField(metadata.MaxLength);
                return;
            }

            // Write empty entries
            writer.WriteField(string.Empty);
            writer.WriteField(0);
        }
    }
}
