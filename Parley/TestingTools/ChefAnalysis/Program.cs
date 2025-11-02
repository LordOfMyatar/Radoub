using System;
using System.IO;
using System.Collections.Generic;
using DialogEditor.Parsers;

namespace ChefAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== CHEF.DLG MYSTERY INVESTIGATION ===");
            Console.WriteLine("Why does chef work perfectly when myra/lista have delays?\n");

            string basePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";

            var files = new Dictionary<string, string>
            {
                { "chef", Path.Combine(basePath, "chef.dlg") },
                { "lista", Path.Combine(basePath, "lista.dlg") },
                { "myra_james", Path.Combine(basePath, "myra_james.dlg") },
                { "generic_hench", Path.Combine(basePath, "generic_hench.dlg") }
            };

            foreach (var file in files)
            {
                if (!File.Exists(file.Value))
                {
                    Console.WriteLine($"❌ {file.Key}: File not found at {file.Value}");
                    continue;
                }

                Console.WriteLine($"\n{'='} Analyzing {file.Key.ToUpper()}.DLG {'='}");
                AnalyzeFile(file.Key, file.Value);
            }

            Console.WriteLine("\n=== ANALYSIS COMPLETE ===");
        }

        static void AnalyzeFile(string name, string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                Console.WriteLine($"📁 File Size: {data.Length:N0} bytes");

                // Parse GFF header
                string signature = System.Text.Encoding.ASCII.GetString(data, 0, 4);
                string version = System.Text.Encoding.ASCII.GetString(data, 4, 4);
                Console.WriteLine($"📝 Signature: {signature} | Version: {version}");

                // Read section offsets
                uint structOffset = BitConverter.ToUInt32(data, 8);
                uint structCount = BitConverter.ToUInt32(data, 12);
                uint fieldOffset = BitConverter.ToUInt32(data, 16);
                uint fieldCount = BitConverter.ToUInt32(data, 20);
                uint labelOffset = BitConverter.ToUInt32(data, 24);
                uint labelCount = BitConverter.ToUInt32(data, 28);
                uint fieldDataOffset = BitConverter.ToUInt32(data, 32);
                uint fieldDataCount = BitConverter.ToUInt32(data, 36);
                uint fieldIndicesOffset = BitConverter.ToUInt32(data, 40);
                uint fieldIndicesCount = BitConverter.ToUInt32(data, 44);
                uint listIndicesOffset = BitConverter.ToUInt32(data, 48);
                uint listIndicesCount = BitConverter.ToUInt32(data, 52);

                Console.WriteLine($"📊 Structs: {structCount} | Fields: {fieldCount} | Labels: {labelCount}");
                Console.WriteLine($"📦 FieldData: {fieldDataCount} bytes | FieldIndices: {fieldIndicesCount} bytes | ListIndices: {listIndicesCount} bytes");

                // Analyze ListIndices section
                Console.WriteLine($"\n🔍 ListIndices Analysis:");
                Console.WriteLine($"   Offset: {listIndicesOffset} | Size: {listIndicesCount} bytes");

                if (listIndicesCount > 0)
                {
                    AnalyzeListIndices(data, listIndicesOffset, listIndicesCount);
                }
                else
                {
                    Console.WriteLine($"   ⚠️ NO ListIndices data (size = 0)");
                }

                // Count ActionParams/ConditionParams fields
                Console.WriteLine($"\n📋 Parameter Fields:");
                CountParamFields(data, fieldOffset, fieldCount, labelOffset, labelCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error analyzing {name}: {ex.Message}");
            }
        }

        static void AnalyzeListIndices(byte[] data, uint offset, uint size)
        {
            uint pos = offset;
            uint endPos = offset + size;
            int listCount = 0;
            List<int> listSizes = new List<int>();

            while (pos < endPos)
            {
                if (pos + 4 > data.Length) break;

                uint count = BitConverter.ToUInt32(data, (int)pos);
                listSizes.Add((int)count);
                listCount++;

                // Skip past count + (count * 4 bytes for struct indices)
                pos += 4 + (count * 4);
            }

            Console.WriteLine($"   Lists found: {listCount}");
            Console.WriteLine($"   List sizes: [{string.Join(", ", listSizes)}]");

            if (listSizes.Count > 0)
            {
                int totalElements = 0;
                foreach (var s in listSizes) totalElements += s;
                double avgSize = totalElements / (double)listSizes.Count;
                Console.WriteLine($"   Total list elements: {totalElements} | Avg: {avgSize:F1}");
            }
        }

        static void CountParamFields(byte[] data, uint fieldOffset, uint fieldCount, uint labelOffset, uint labelCount)
        {
            // Read all labels first
            var labels = new List<string>();
            uint labelPos = labelOffset;
            for (int i = 0; i < labelCount; i++)
            {
                string label = ReadNullTerminatedString(data, labelPos, out int bytesRead);
                labels.Add(label);
                labelPos += (uint)bytesRead;
            }

            // Count ActionParams and ConditionParams fields
            int actionParamsCount = 0;
            int conditionParamsCount = 0;
            int actionParamsWithData = 0;
            int conditionParamsWithData = 0;

            uint fieldPos = fieldOffset;
            for (int i = 0; i < fieldCount; i++)
            {
                // Field structure: Type(4) + LabelIndex(4) + DataOrDataOffset(4) = 12 bytes
                uint type = BitConverter.ToUInt32(data, (int)fieldPos);
                uint labelIndex = BitConverter.ToUInt32(data, (int)fieldPos + 4);
                uint dataOrOffset = BitConverter.ToUInt32(data, (int)fieldPos + 8);

                if (labelIndex < labels.Count)
                {
                    string label = labels[(int)labelIndex];

                    if (label == "ActionParams")
                    {
                        actionParamsCount++;
                        if (dataOrOffset != 0xFFFFFFFF)
                        {
                            actionParamsWithData++;
                        }
                    }
                    else if (label == "ConditionParams")
                    {
                        conditionParamsCount++;
                        if (dataOrOffset != 0xFFFFFFFF)
                        {
                            conditionParamsWithData++;
                        }
                    }
                }

                fieldPos += 12;
            }

            Console.WriteLine($"   ActionParams: {actionParamsCount} total | {actionParamsWithData} with data (non-0xFFFFFFFF)");
            Console.WriteLine($"   ConditionParams: {conditionParamsCount} total | {conditionParamsWithData} with data (non-0xFFFFFFFF)");

            int totalParamsWithData = actionParamsWithData + conditionParamsWithData;
            Console.WriteLine($"   ⭐ Total param fields with data: {totalParamsWithData}");
        }

        static string ReadNullTerminatedString(byte[] data, uint offset, out int bytesRead)
        {
            List<byte> bytes = new List<byte>();
            uint pos = offset;

            while (pos < data.Length && data[pos] != 0)
            {
                bytes.Add(data[pos]);
                pos++;
            }

            bytesRead = (int)(pos - offset) + 1; // Include null terminator
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
