using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Converts between Radoub.Formats.Dlg models and Parley's Dialog models.
    /// Parley's models have UI-specific features (LinkRegistry, node pooling, bidirectional refs)
    /// while Radoub.Formats models are simple POCOs for file I/O.
    ///
    /// TLK Resolution: Uses GameResourceService.Instance to resolve StrRef values to
    /// actual text from the game's TLK files. This is essential for displaying dialog
    /// text that references the TLK rather than containing inline text.
    /// </summary>
    public static class DlgAdapter
    {
        #region DlgFile -> Dialog (Reading)

        /// <summary>
        /// Convert a Radoub.Formats DlgFile to Parley's Dialog model.
        /// Automatically resolves TLK StrRef values to display text.
        /// </summary>
        public static Dialog ToDialog(DlgFile dlgFile)
        {
            var dialog = new Dialog
            {
                DelayEntry = dlgFile.DelayEntry,
                DelayReply = dlgFile.DelayReply,
                NumWords = dlgFile.NumWords,
                ScriptEnd = dlgFile.EndConversation,
                ScriptAbort = dlgFile.EndConverAbort,
                PreventZoom = dlgFile.PreventZoomIn
            };

            // First pass: Create all Entry nodes
            foreach (var dlgEntry in dlgFile.Entries)
            {
                var node = dialog.CreateNode(DialogNodeType.Entry);
                if (node == null) continue;

                node.Speaker = dlgEntry.Speaker;
                node.Animation = (DialogAnimation)dlgEntry.Animation;
                node.AnimationLoop = dlgEntry.AnimLoop;
                node.Text = ConvertLocString(dlgEntry.Text);
                node.ScriptAction = dlgEntry.Script;
                node.Delay = dlgEntry.Delay;
                node.Comment = dlgEntry.Comment;
                node.Sound = dlgEntry.Sound;
                node.Quest = dlgEntry.Quest;
                node.QuestEntry = dlgEntry.QuestEntry;

                // Convert ActionParams
                foreach (var param in dlgEntry.ActionParams)
                {
                    node.ActionParams[param.Key] = param.Value;
                }

                dialog.AddNodeInternal(node, DialogNodeType.Entry);
            }

            // Second pass: Create all Reply nodes
            foreach (var dlgReply in dlgFile.Replies)
            {
                var node = dialog.CreateNode(DialogNodeType.Reply);
                if (node == null) continue;

                node.Animation = (DialogAnimation)dlgReply.Animation;
                node.AnimationLoop = dlgReply.AnimLoop;
                node.Text = ConvertLocString(dlgReply.Text);
                node.ScriptAction = dlgReply.Script;
                node.Delay = dlgReply.Delay;
                node.Comment = dlgReply.Comment;
                node.Sound = dlgReply.Sound;
                node.Quest = dlgReply.Quest;
                node.QuestEntry = dlgReply.QuestEntry;

                // Convert ActionParams
                foreach (var param in dlgReply.ActionParams)
                {
                    node.ActionParams[param.Key] = param.Value;
                }

                dialog.AddNodeInternal(node, DialogNodeType.Reply);
            }

            // Third pass: Wire up Entry -> Reply links (RepliesList)
            for (int i = 0; i < dlgFile.Entries.Count; i++)
            {
                var dlgEntry = dlgFile.Entries[i];
                var dialogEntry = dialog.Entries[i];

                foreach (var link in dlgEntry.RepliesList)
                {
                    var ptr = dialog.CreatePtr();
                    if (ptr == null) continue;

                    ptr.Type = DialogNodeType.Reply;
                    ptr.Index = link.Index;
                    ptr.ScriptAppears = link.Active;
                    ptr.IsLink = link.IsChild;
                    ptr.LinkComment = link.LinkComment;

                    // Convert ConditionParams
                    foreach (var param in link.ConditionParams)
                    {
                        ptr.ConditionParams[param.Key] = param.Value;
                    }

                    // Resolve node reference
                    if (link.Index < dialog.Replies.Count)
                    {
                        ptr.Node = dialog.Replies[(int)link.Index];
                    }

                    dialogEntry.Pointers.Add(ptr);
                }
            }

            // Fourth pass: Wire up Reply -> Entry links (EntriesList)
            for (int i = 0; i < dlgFile.Replies.Count; i++)
            {
                var dlgReply = dlgFile.Replies[i];
                var dialogReply = dialog.Replies[i];

                foreach (var link in dlgReply.EntriesList)
                {
                    var ptr = dialog.CreatePtr();
                    if (ptr == null) continue;

                    ptr.Type = DialogNodeType.Entry;
                    ptr.Index = link.Index;
                    ptr.ScriptAppears = link.Active;
                    ptr.IsLink = link.IsChild;
                    ptr.LinkComment = link.LinkComment;

                    // Convert ConditionParams
                    foreach (var param in link.ConditionParams)
                    {
                        ptr.ConditionParams[param.Key] = param.Value;
                    }

                    // Resolve node reference
                    if (link.Index < dialog.Entries.Count)
                    {
                        ptr.Node = dialog.Entries[(int)link.Index];
                    }

                    dialogReply.Pointers.Add(ptr);
                }
            }

            // Fifth pass: Wire up StartingList
            foreach (var startLink in dlgFile.StartingList)
            {
                var ptr = dialog.CreatePtr();
                if (ptr == null) continue;

                ptr.Type = DialogNodeType.Entry;
                ptr.Index = startLink.Index;
                ptr.ScriptAppears = startLink.Active;
                ptr.IsLink = startLink.IsChild;
                ptr.IsStart = true;
                ptr.LinkComment = startLink.LinkComment;

                // Convert ConditionParams
                foreach (var param in startLink.ConditionParams)
                {
                    ptr.ConditionParams[param.Key] = param.Value;
                }

                // Resolve node reference
                if (startLink.Index < dialog.Entries.Count)
                {
                    ptr.Node = dialog.Entries[(int)startLink.Index];
                }

                dialog.Starts.Add(ptr);
            }

            // Rebuild the link registry
            dialog.RebuildLinkRegistry();

            return dialog;
        }

        private static LocString ConvertLocString(CExoLocString cexo)
        {
            var locString = new LocString
            {
                StrRef = cexo.StrRef
            };

            // Check if text needs TLK resolution
            if (cexo.LocalizedStrings.Count == 0 && cexo.StrRef != 0xFFFFFFFF)
            {
                // No inline text but valid StrRef - resolve from TLK
                var tlkText = GameResourceService.Instance.GetTlkString(cexo.StrRef);
                if (tlkText != null)
                {
                    locString.Strings[0] = tlkText;
                    locString.DefaultText = tlkText;
                    UnifiedLogger.LogParser(LogLevel.DEBUG,
                        $"TLK resolved: StrRef={cexo.StrRef} → '{(tlkText.Length > 50 ? tlkText.Substring(0, 50) + "..." : tlkText)}'");
                }
                else
                {
                    // TLK lookup failed - show placeholder
                    var placeholder = $"<StrRef:{cexo.StrRef}>";
                    locString.Strings[0] = placeholder;
                    locString.DefaultText = placeholder;
                    UnifiedLogger.LogParser(LogLevel.WARN,
                        $"TLK lookup failed: StrRef={cexo.StrRef} (TLK may not be loaded)");
                }
            }
            else if (cexo.LocalizedStrings.Count > 0)
            {
                // Check for embedded StrRef placeholders (from files saved with unresolved StrRefs)
                var firstText = cexo.LocalizedStrings.Values.FirstOrDefault();
                if (firstText != null && firstText.StartsWith("<StrRef:") && firstText.EndsWith(">"))
                {
                    // Parse and resolve the embedded StrRef
                    var strRefText = firstText.Substring(8, firstText.Length - 9);
                    if (uint.TryParse(strRefText, out var embeddedStrRef))
                    {
                        var tlkText = GameResourceService.Instance.GetTlkString(embeddedStrRef);
                        if (tlkText != null)
                        {
                            locString.Strings[0] = tlkText;
                            locString.DefaultText = tlkText;
                            UnifiedLogger.LogParser(LogLevel.DEBUG,
                                $"TLK resolved embedded: StrRef={embeddedStrRef} → '{(tlkText.Length > 50 ? tlkText.Substring(0, 50) + "..." : tlkText)}'");
                        }
                        else
                        {
                            // Keep original placeholder
                            foreach (var kvp in cexo.LocalizedStrings)
                            {
                                locString.Strings[(int)kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    else
                    {
                        // Not a valid StrRef number, keep original
                        foreach (var kvp in cexo.LocalizedStrings)
                        {
                            locString.Strings[(int)kvp.Key] = kvp.Value;
                        }
                    }
                }
                else
                {
                    // Normal inline text - copy as-is
                    foreach (var kvp in cexo.LocalizedStrings)
                    {
                        locString.Strings[(int)kvp.Key] = kvp.Value;
                    }

                    // Set DefaultText from English (0) if available
                    if (cexo.LocalizedStrings.TryGetValue(0, out var english))
                    {
                        locString.DefaultText = english;
                    }
                }
            }
            // else: Empty text is valid - "[CONTINUE]" nodes have no text

            return locString;
        }

        #endregion

        #region Dialog -> DlgFile (Writing)

        /// <summary>
        /// Convert Parley's Dialog model to Radoub.Formats DlgFile.
        /// </summary>
        public static DlgFile ToDlgFile(Dialog dialog)
        {
            var dlgFile = new DlgFile
            {
                DelayEntry = dialog.DelayEntry,
                DelayReply = dialog.DelayReply,
                NumWords = dialog.NumWords,
                EndConversation = dialog.ScriptEnd,
                EndConverAbort = dialog.ScriptAbort,
                PreventZoomIn = dialog.PreventZoom
            };

            // Build index maps for pointer resolution
            var entryIndexMap = new Dictionary<DialogNode, uint>();
            var replyIndexMap = new Dictionary<DialogNode, uint>();

            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                entryIndexMap[dialog.Entries[i]] = (uint)i;
            }

            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                replyIndexMap[dialog.Replies[i]] = (uint)i;
            }

            // Convert Entries
            foreach (var dialogEntry in dialog.Entries)
            {
                var entry = new DlgEntry
                {
                    Speaker = dialogEntry.Speaker,
                    Animation = (uint)dialogEntry.Animation,
                    AnimLoop = dialogEntry.AnimationLoop,
                    Text = ConvertLocStringToCExo(dialogEntry.Text),
                    Script = dialogEntry.ScriptAction,
                    Delay = dialogEntry.Delay,
                    Comment = dialogEntry.Comment,
                    Sound = dialogEntry.Sound,
                    Quest = dialogEntry.Quest,
                    QuestEntry = dialogEntry.QuestEntry
                };

                // Convert ActionParams
                foreach (var kvp in dialogEntry.ActionParams)
                {
                    entry.ActionParams.Add(new DlgParam { Key = kvp.Key, Value = kvp.Value });
                }

                // Convert RepliesList (Entry -> Reply links)
                foreach (var ptr in dialogEntry.Pointers)
                {
                    var link = new DlgLink
                    {
                        Index = ptr.Node != null && replyIndexMap.TryGetValue(ptr.Node, out var idx)
                            ? idx
                            : ptr.Index,
                        Active = ptr.ScriptAppears,
                        IsChild = ptr.IsLink,
                        LinkComment = ptr.LinkComment
                    };

                    // Convert ConditionParams
                    foreach (var kvp in ptr.ConditionParams)
                    {
                        link.ConditionParams.Add(new DlgParam { Key = kvp.Key, Value = kvp.Value });
                    }

                    entry.RepliesList.Add(link);
                }

                dlgFile.Entries.Add(entry);
            }

            // Convert Replies
            foreach (var dialogReply in dialog.Replies)
            {
                var reply = new DlgReply
                {
                    Animation = (uint)dialogReply.Animation,
                    AnimLoop = dialogReply.AnimationLoop,
                    Text = ConvertLocStringToCExo(dialogReply.Text),
                    Script = dialogReply.ScriptAction,
                    Delay = dialogReply.Delay,
                    Comment = dialogReply.Comment,
                    Sound = dialogReply.Sound,
                    Quest = dialogReply.Quest,
                    QuestEntry = dialogReply.QuestEntry
                };

                // Convert ActionParams
                foreach (var kvp in dialogReply.ActionParams)
                {
                    reply.ActionParams.Add(new DlgParam { Key = kvp.Key, Value = kvp.Value });
                }

                // Convert EntriesList (Reply -> Entry links)
                foreach (var ptr in dialogReply.Pointers)
                {
                    var link = new DlgLink
                    {
                        Index = ptr.Node != null && entryIndexMap.TryGetValue(ptr.Node, out var idx)
                            ? idx
                            : ptr.Index,
                        Active = ptr.ScriptAppears,
                        IsChild = ptr.IsLink,
                        LinkComment = ptr.LinkComment
                    };

                    // Convert ConditionParams
                    foreach (var kvp in ptr.ConditionParams)
                    {
                        link.ConditionParams.Add(new DlgParam { Key = kvp.Key, Value = kvp.Value });
                    }

                    reply.EntriesList.Add(link);
                }

                dlgFile.Replies.Add(reply);
            }

            // Convert StartingList
            foreach (var startPtr in dialog.Starts)
            {
                var link = new DlgLink
                {
                    Index = startPtr.Node != null && entryIndexMap.TryGetValue(startPtr.Node, out var idx)
                        ? idx
                        : startPtr.Index,
                    Active = startPtr.ScriptAppears,
                    IsChild = startPtr.IsLink,
                    LinkComment = startPtr.LinkComment
                };

                // Convert ConditionParams
                foreach (var kvp in startPtr.ConditionParams)
                {
                    link.ConditionParams.Add(new DlgParam { Key = kvp.Key, Value = kvp.Value });
                }

                dlgFile.StartingList.Add(link);
            }

            return dlgFile;
        }

        private static CExoLocString ConvertLocStringToCExo(LocString locString)
        {
            var cexo = new CExoLocString
            {
                StrRef = locString.StrRef
            };

            foreach (var kvp in locString.Strings)
            {
                cexo.LocalizedStrings[(uint)kvp.Key] = kvp.Value;
            }

            return cexo;
        }

        #endregion
    }
}
