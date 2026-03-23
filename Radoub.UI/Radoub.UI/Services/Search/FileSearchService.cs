using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Radoub.UI.Services.Search
{
    /// <summary>
    /// Generic single-file search service that wraps any IFileSearchProvider.
    /// Reads the file from disk, searches via the provider, and maintains match navigation state.
    /// </summary>
    public class FileSearchService
    {
        private readonly IFileSearchProvider _provider;
        private IReadOnlyList<SearchMatch> _currentMatches = Array.Empty<SearchMatch>();
        private int _currentIndex = -1;

        public FileSearchService(IFileSearchProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Current search results</summary>
        public IReadOnlyList<SearchMatch> Matches => _currentMatches;

        /// <summary>Index of the currently selected match (-1 if none)</summary>
        public int CurrentIndex => _currentIndex;

        /// <summary>Total number of matches</summary>
        public int MatchCount => _currentMatches.Count;

        /// <summary>Get the current match without changing position.</summary>
        public SearchMatch? CurrentMatch =>
            _currentIndex >= 0 && _currentIndex < _currentMatches.Count
                ? _currentMatches[_currentIndex]
                : null;

        /// <summary>
        /// Search the specified file for matches.
        /// Reads the file fresh from disk to get a GffFile for the search provider.
        /// </summary>
        public int Search(string filePath, SearchCriteria criteria)
        {
            _currentMatches = Array.Empty<SearchMatch>();
            _currentIndex = -1;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return 0;

            var validationError = criteria.Validate();
            if (validationError != null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Invalid search pattern: {validationError}");
                return 0;
            }

            try
            {
                var gffFile = GffReader.Read(filePath);
                _currentMatches = _provider.Search(gffFile, criteria);
                _currentIndex = _currentMatches.Count > 0 ? 0 : -1;
                return _currentMatches.Count;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Search failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Move to the next match. Wraps around to the beginning.</summary>
        public SearchMatch? NextMatch()
        {
            if (_currentMatches.Count == 0) return null;
            _currentIndex = (_currentIndex + 1) % _currentMatches.Count;
            return _currentMatches[_currentIndex];
        }

        /// <summary>Move to the previous match. Wraps around to the end.</summary>
        public SearchMatch? PreviousMatch()
        {
            if (_currentMatches.Count == 0) return null;
            _currentIndex = (_currentIndex - 1 + _currentMatches.Count) % _currentMatches.Count;
            return _currentMatches[_currentIndex];
        }

        /// <summary>Clear all search results.</summary>
        public void Clear()
        {
            _currentMatches = Array.Empty<SearchMatch>();
            _currentIndex = -1;
        }

        /// <summary>
        /// Replace the current match in the file on disk.
        /// Returns the result, and re-runs search to update matches.
        /// </summary>
        public ReplaceResult? ReplaceCurrent(string filePath, string replacementText, SearchCriteria criteria)
        {
            var match = CurrentMatch;
            if (match == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var gffFile = GffReader.Read(filePath);
                var ops = new[] { new ReplaceOperation { Match = match, ReplacementText = replacementText } };
                var results = _provider.Replace(gffFile, ops);
                var result = results.FirstOrDefault();

                if (result?.Success == true)
                {
                    var bytes = GffWriter.Write(gffFile);
                    File.WriteAllBytes(filePath, bytes);
                    Search(filePath, criteria);
                }

                return result;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Replace failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Replace all current matches in the file on disk.
        /// Returns the number of successful replacements.
        /// </summary>
        public int ReplaceAll(string filePath, string replacementText, SearchCriteria criteria)
        {
            if (_currentMatches.Count == 0 || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return 0;

            try
            {
                var gffFile = GffReader.Read(filePath);
                var ops = _currentMatches.Select(m => new ReplaceOperation
                {
                    Match = m,
                    ReplacementText = replacementText
                }).ToList();

                var results = _provider.Replace(gffFile, ops);
                var successCount = results.Count(r => r.Success);

                if (successCount > 0)
                {
                    var bytes = GffWriter.Write(gffFile);
                    File.WriteAllBytes(filePath, bytes);
                    Search(filePath, criteria);
                }

                return successCount;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Replace all failed: {ex.Message}");
                return 0;
            }
        }
    }
}
