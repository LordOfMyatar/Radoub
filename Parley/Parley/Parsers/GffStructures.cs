using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    public class GffHeader
    {
        public string FileType { get; set; } = string.Empty;      // First 4 bytes as string
        public string FileVersion { get; set; } = string.Empty;   // Next 4 bytes as string
        public uint StructOffset { get; set; }
        public uint StructCount { get; set; }
        public uint FieldOffset { get; set; }
        public uint FieldCount { get; set; }
        public uint LabelOffset { get; set; }
        public uint LabelCount { get; set; }
        public uint FieldDataOffset { get; set; }
        public uint FieldDataCount { get; set; }
        public uint FieldIndicesOffset { get; set; }
        public uint FieldIndicesCount { get; set; }
        public uint ListIndicesOffset { get; set; }
        public uint ListIndicesCount { get; set; }
        
        // 2025-09-12: Pattern detection properties
        public string DetectedAuroraPattern { get; set; } = string.Empty;
        public double FieldIndicesRatio { get; set; }
    }

    public class GffStruct
    {
        public uint Type { get; set; }
        public uint DataOrDataOffset { get; set; }
        public uint FieldCount { get; set; }
        public List<GffField> Fields { get; set; } = new();
        
        public GffField? GetField(string label)
        {
            return Fields.FirstOrDefault(f => f.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
        }
        
        public T GetFieldValue<T>(string label, T defaultValue = default!)
        {
            var field = GetField(label);
            if (field?.Value is T value)
                return value;

            // Debug type mismatch for Animation field
            if (label == "Animation" && field != null)
            {
                UnifiedLogger.LogParser(LogLevel.INFO,
                    $"GetFieldValue TYPE MISMATCH: label={label}, field.Value type={field.Value?.GetType().Name ?? "null"}, requested type={typeof(T).Name}, field.Value={field.Value}");
            }

            return defaultValue;
        }
    }

    public class GffField
    {
        public uint Type { get; set; }
        public uint LabelIndex { get; set; }
        public string Label { get; set; } = string.Empty;
        public uint DataOrDataOffset { get; set; }
        public object? Value { get; set; }
        
        // Field type constants from GFF specification
        public const uint BYTE = 0;
        public const uint CHAR = 1;
        public const uint WORD = 2;
        public const uint SHORT = 3;
        public const uint DWORD = 4;
        public const uint INT = 5;
        public const uint DWORD64 = 6;
        public const uint INT64 = 7;
        public const uint FLOAT = 8;
        public const uint DOUBLE = 9;
        public const uint CExoString = 10;
        public const uint CResRef = 11;
        public const uint CExoLocString = 12;
        public const uint VOID = 13;
        public const uint Struct = 14;
        public const uint List = 15;
        
        public bool IsByte => Type == BYTE;
        public bool IsChar => Type == CHAR;
        public bool IsWord => Type == WORD;
        public bool IsShort => Type == SHORT;
        public bool IsDWord => Type == DWORD;
        public bool IsInt => Type == INT;
        public bool IsFloat => Type == FLOAT;
        public bool IsDouble => Type == DOUBLE;
        public bool IsCExoString => Type == CExoString;
        public bool IsCResRef => Type == CResRef;
        public bool IsCExoLocString => Type == CExoLocString;
        public bool IsStruct => Type == Struct;
        public bool IsList => Type == List;
    }

    public class GffLabel
    {
        public string Text { get; set; } = string.Empty;
    }

    public class GffList
    {
        public uint Count { get; set; }
        public List<GffStruct> Elements { get; set; } = new();
    }

    public class CExoLocString
    {
        public uint StrRef { get; set; } = 0xFFFFFFFF;
        public uint SubStringCount { get; set; }
        public Dictionary<uint, string> LocalizedStrings { get; set; } = new();
        
        public string GetString(uint languageId = 0)
        {
            return LocalizedStrings.TryGetValue(languageId, out var text) ? text : string.Empty;
        }
        
        public string GetDefaultString()
        {
            // Try English first (0), then any available language
            if (LocalizedStrings.TryGetValue(0, out var english))
                return english;
                
            return LocalizedStrings.Values.FirstOrDefault() ?? string.Empty;
        }
        
        public bool IsEmpty => LocalizedStrings.Count == 0 && StrRef == 0xFFFFFFFF;
    }

    public static class GffFieldTypeExtensions
    {
        public static bool IsSimpleType(this uint fieldType)
        {
            return fieldType switch
            {
                GffField.BYTE or GffField.CHAR or GffField.WORD or GffField.SHORT or
                GffField.DWORD or GffField.INT or GffField.DWORD64 or GffField.INT64 or
                GffField.FLOAT or GffField.DOUBLE => true,
                _ => false
            };
        }

        public static bool IsComplexType(this uint fieldType)
        {
            return fieldType switch
            {
                GffField.CExoString or GffField.CResRef or GffField.CExoLocString or
                GffField.VOID or GffField.Struct or GffField.List => true,
                _ => false
            };
        }

        public static string GetTypeName(this uint fieldType)
        {
            return fieldType switch
            {
                GffField.BYTE => "BYTE",
                GffField.CHAR => "CHAR", 
                GffField.WORD => "WORD",
                GffField.SHORT => "SHORT",
                GffField.DWORD => "DWORD",
                GffField.INT => "INT",
                GffField.DWORD64 => "DWORD64",
                GffField.INT64 => "INT64",
                GffField.FLOAT => "FLOAT",
                GffField.DOUBLE => "DOUBLE",
                GffField.CExoString => "CExoString",
                GffField.CResRef => "CResRef",
                GffField.CExoLocString => "CExoLocString",
                GffField.VOID => "VOID",
                GffField.Struct => "Struct",
                GffField.List => "List",
                _ => $"Unknown({fieldType})"
            };
        }
    }
}