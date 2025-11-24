# PowerShell script to remove duplicate methods that are now in services
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$content = Get-Content $filePath -Raw

# Remove UpdateConditionParamsFromUI (now in ScriptParameterUIManager)
$content = $content -replace 'private void UpdateConditionParamsFromUI\(DialogPtr ptr, string\? scriptName = null\)[\s\S]*?UnifiedLogger\.LogApplication\(LogLevel\.DEBUG, \$"UpdateConditionParamsFromUI: EXIT - ptr now has \{ptr\.ConditionParams\.Count\} params"\);[\s\S]*?\}[\s\S]*?\r?\n\r?\n', ''

# Remove UpdateActionParamsFromUI (now in ScriptParameterUIManager)
$content = $content -replace 'private void UpdateActionParamsFromUI\(DialogNode node, string\? scriptName = null\)[\s\S]*?UnifiedLogger\.LogApplication\(LogLevel\.DEBUG, \$"UpdateActionParamsFromUI: EXIT - node ''\{node\.DisplayText\}'' now has \{node\.ActionParams\.Count\} params"\);[\s\S]*?\}[\s\S]*?\r?\n\r?\n', ''

# Remove FindLastAddedNode (now in NodeCreationHelper)
$content = $content -replace 'private TreeViewSafeNode\? FindLastAddedNode\(TreeView treeView, bool entryAdded, bool replyAdded\)[\s\S]*?return null;[\s\S]*?\}[\s\S]*?\r?\n\r?\n', ''

# Remove FindLastAddedNodeRecursive (now in NodeCreationHelper)
$content = $content -replace 'private TreeViewSafeNode\? FindLastAddedNodeRecursive\(TreeViewSafeNode node, bool entryAdded, bool replyAdded\)[\s\S]*?return null;[\s\S]*?\}[\s\S]*?\r?\n\r?\n', ''

# Remove ExpandToNode (now in NodeCreationHelper)
$content = $content -replace '/// <summary>[\s\S]*?/// Handles "collapse all" scenario by expanding entire path from root to node[\s\S]*?/// </summary>[\s\S]*?private void ExpandToNode\(TreeView treeView, TreeViewSafeNode targetNode\)[\s\S]*?ancestor\.IsExpanded = true;[\s\S]*?\}[\s\S]*?\}[\s\S]*?\r?\n\r?\n', ''

# Write back
$content | Set-Content $filePath -NoNewline

Write-Host "Duplicate methods cleanup completed!"
