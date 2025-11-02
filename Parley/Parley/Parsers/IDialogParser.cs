using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;

namespace DialogEditor.Parsers
{
    public interface IDialogParser
    {
        Task<Dialog?> ParseFromFileAsync(string filePath);
        Task<Dialog?> ParseFromStreamAsync(Stream stream);
        Task<Dialog?> ParseFromJsonAsync(string jsonContent);
        
        Task<bool> WriteToFileAsync(Dialog dialog, string filePath);
        Task<bool> WriteToStreamAsync(Dialog dialog, Stream stream);
        Task<string> WriteToJsonAsync(Dialog dialog);
        
        bool IsValidDlgFile(string filePath);
        ParserResult ValidateStructure(Dialog dialog);
    }
    
    public class ParserResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public List<string> Warnings { get; set; } = new();
        
        public static ParserResult CreateSuccess()
        {
            return new ParserResult { Success = true };
        }
        
        public static ParserResult CreateError(string message, Exception? exception = null)
        {
            return new ParserResult 
            { 
                Success = false, 
                ErrorMessage = message, 
                Exception = exception 
            };
        }
        
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}