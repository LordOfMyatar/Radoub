using System;
using System.Collections.Generic;

namespace DialogEditor.Models
{
    /// <summary>
    /// Result object for parser and validator operations.
    /// Contains success status, error information, and warnings.
    /// </summary>
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
