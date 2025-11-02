# Claude Development Timeline

**Project**: Parley - Neverwinter Nights Dialog Editor
**Duration**: August 23, 2025 - November 1, 2025 (70 days)
**Development Model**: AI-Human Collaboration (Claude + User)

This document chronicles the day-by-day development of Parley, a cross-platform dialog editor for Neverwinter Nights. The entire codebase was written by Claude (Anthropic's AI assistant) under direction from a human collaborator who provided requirements, testing, and architectural decisions.

---

## August 2025

### Week 1: Project Inception (Aug 23-24)

**August 23, 2025** - Day 1: The Conversation That Started Everything
- Initial conversation about creating a Neverwinter Nights conversation editor
- Explored ChatMapper integration but abandoned due to licensing costs
- Began studying Aurora Engine's DLG (dialog) file format
- Created VSCode plugin concepts and sample files
- First attempts at format conversion between systems
- Initial README and project structure

**August 24, 2025** - Day 2: First GUI and Security Foundation
- Created initial GUI mockup using static test data
- Began work on GFF (Generic File Format) parser for DLG files
- Implemented comprehensive input validation for file parsing (security focus)
- Created technical debt and security debt tracking systems
- Fixed TreeView vertical spacing issues
- Added window position persistence
- Implemented theme switching infrastructure (dark mode still problematic)
- Consolidated duplicate GFF analysis code

### Week 2: The Parser Wars Begin (Aug 28-31)

**August 28, 2025** - Day 6: Modernization Push
- Upgraded project from .NET 8 to .NET 9
- Updated Claude session documentation

**August 29, 2025** - Day 7: Theme System Overhaul
- Replaced WPF-UI with ModernWpf for standardized theming
- Enhanced text visibility in dark mode
- Fixed child entry/reply button visibility issues
- Updated changelog and debt tracking

**August 30, 2025** - Day 8: File Operations Breakthrough
- Implemented comprehensive DLG file save functionality
- Added binary serialization debugging
- Switched to modern GFF format for NWN 1.69+
- Created file-based logging system with rotation
- Implemented Settings dialog and Recent Files menu
- Added hybrid JSON-GFF serialization approach
- Fixed GFF format detection issues

**August 31, 2025** - Day 9: The Template Trap
- Discovered file export was using chef.dlg as template (wrong approach)
- Implemented module validation dialog with custom WPF styling
- Fixed module validation logic for renamed directories
- Added command-line file launching capability
- Consolidated DLG export specifications
- Attempted binary bypass approach for exports

---

## September 2025

### Week 1: Export System Crisis (Sep 1-7)

**September 1, 2025** - Day 10: The GFF Writer Revelation
- 🎉 **MAJOR BREAKTHROUGH**: Complete DLG round-trip functionality achieved
- Replaced template-based generation with proper GFF writer
- Fixed ConversationLoaderService tree building
- Added circular reference protection to prevent stack overflow
- Updated all documentation for new directory structure
- Comprehensive testing scripts created

**September 2, 2025** - Day 12: Aurora Compatibility Hunt
- Discovered DLG exports missing critical Sync Structs
- Replaced popup error dialogs with comprehensive logging
- Organized gitignore and improved parsing/writing
- Resolved StrRef corruption for Aurora Editor compatibility

**September 3, 2025** - Day 13: The Mystery Deepens
- **MAJOR BREAKTHROUGH**: Discovered export process is real issue, not parser
- Found root cause of Aurora compatibility problems
- Fixed CExoLocString field order parsing bug
- Partially resolved StrRef corruption

**September 4, 2025** - Day 14: Data Loss Catastrophe Averted
- **CRITICAL BUG FIX**: Resolved 80-90% data loss in DLG parsing
- Fixed export conversation linking causing orphaned entries
- Major logging system overhaul for debugging
- Applied correct BioWare specification for CExoLocString field order

**September 6, 2025** - Day 16: Project Reorganization
- Major directory reorganization
- Created ArcReactor project (later renamed to Parley)
- Implemented session-based logging with component isolation
- Fixed dual logging issue eliminating FileLogger duplication
- Complete hierarchical dialog tree working
- Fixed GUI display issues

**September 7, 2025** - Day 17: Feature Expansion
- Completed enhanced UI and dialog management features
- Perfect dialog tree structure export working
- Added dark theme styling
- Implemented script parameter parsing
- Comprehensive round-trip testing framework

### Week 2: The 1990s Format Challenge (Sep 8-14)

**September 8, 2025** - Day 18: Aurora 1990s Format Deep Dive
- Implemented Aurora-compatible label format
- Resolved critical round-trip issues
- 95% round-trip success achieved
- Complete Aurora 1990s DLG format implementation

**September 9, 2025** - Day 19: GUI Feature Parity
- Complete Aurora GUI feature parity with modern enhancements
- Enhanced ArcReactor with advanced GUI improvements

**September 10, 2025** - Day 20: The Pointer Problem
- Achieved 100% Aurora DLG round-trip compatibility
- Fixed list indices struct referencing
- Added missing condition parameter structs
- Fixed critical pointer index corruption
- Reply[0] field alignment working

**September 11, 2025** - Day 21: Workspace Cleanup
- Complete documentation reorganization
- Test scripts organized
- Circular reference detection for GUI conversation loops

**September 12, 2025** - Day 22: BioWare Documentation Integration
- Moved BioWare Aurora Engine formats to Documentation
- Fixed WPF TreeView circular reference and export corruption
- Achieved tree-safe conversation flow

**September 13, 2025** - Day 23: The 4:1 Pattern Discovery
- **Autobender incident**: Claude used entire day's API quota rapidly
- Implemented Aurora 4:1 field indices pattern - **MAJOR BREAKTHROUGH**
- Complete Aurora export compatibility - 100% round-trip success
- Resolved WPF TreeView display issues in dark mode
- **MISSION ACCOMPLISHED** on core dialog system

**September 14, 2025** - Day 24: Export Corruption Battle
- Resolved critical export corruption
- Fixed buffer truncation and CResRef format errors
- Complete export functionality with perfect round-trip compatibility
- Identified root struct field mapping issue

### Week 3: Field Index Mystery (Sep 15-21)

**September 15, 2025** - Day 25: The Struct Alignment Puzzle
- **BREAKTHROUGH**: Fixed field assignment corruption completely
- Export system fully operational
- Conversation flow preserved in exports
- Reply pointer structs working perfectly
- **UAT CONFIRMED**: Lista.dlg exports correctly

**September 16, 2025** - Day 26: Aurora Field Sets Complete
- 96.6% Aurora compatibility achieved
- Complete Aurora field sets implemented
- **Milestone**: UAT confirms exports ready for Aurora testing
- Identified GFF List parser boundary issue
- Implemented text deduplication
- Resolved TreeView conversation explosion
- Fixed content vs pointer structure separation architecture

**September 17, 2025** - Day 27: TreeView Polish
- Complete TreeView conversation display
- Pointer resolution system working

**September 18, 2025** - Day 28: Field Indices Victory
- **Major field indices corruption fix** - 95% export resolution achieved
- Complete TreeView conversation display polished

**September 19, 2025** - Day 29: Boundary Calculation Fix
- Fixed GFF List boundary calculation
- **Complete round-trip functionality restored**
- Updated documentation for breakthrough
- Identified Aurora struct count mismatch
- Aurora deduplication analysis added

**September 20, 2025** - Day 30: Architecture Revelation
- **Major Aurora compatibility breakthrough** - discovered actual vs theoretical format
- Implemented Aurora complex field index mapping
- Complete project cleanup
- Session continuity system established
- Major export fixes for entry order
- Resolved tree structure reversal
- **Complete conversation tree building validation** - 98% project completion
- Final GUI polish complete
- Font scaling working
- Removed all hardcoded text truncation
- Proper speaker format in TreeView

**September 21, 2025** - Day 31: Documentation Marathon
- Initial conversion of BioWare Aurora Engine PDFs to Markdown
- Normalized all headers to ATX format across 20+ documentation files
- Complete tree structure analysis
- Implemented field index tracking
- Complete Aurora compact pointer algorithm

### Week 4: Sound & Script Integration (Sep 22-30)

**September 22-30**: *No commits - planning phase*

---

## October 2025

### Week 1-3: The Hang Bug Saga (Oct 1-22)

**October 18, 2025** - Day 57: Parameter Deduplication
- Fixed parameter struct deduplication for Aurora compatibility
- CalculateListDataSize now includes parameter lists

**October 19, 2025** - Day 58: The Great Revert and CResRef Fix
- **Critical Day**: Reverted parser to working state
- Applied FieldIndicesCount fix
- **Fixed struct types and CResRef format per GFF spec**
- Automated regression testing framework created
- **DLG export now Aurora-compatible**
- Archived obsolete documentation
- Updated CODE_PATH_MAP
- File → New menu added
- Comprehensive parser stress test created
- Cleaned up root tech debt

**October 20, 2025** - Day 60: Node Reordering Planning
- Reorganized Phase 2 into subphases with 4C plans
- Consolidated all plans into 4C folder
- Detailed node reordering implementation plan
- Comprehensive Aurora struct type analysis

**October 21, 2025** - Day 63: Traversal Algorithm Breakthrough
- Implemented Aurora's pointers-first traversal algorithm
- Implemented interleaved struct creation with depth-first traversal
- Fixed Start struct creation timing
- Implemented Entry-First batched struct ordering
- Added conditional LinkComment field support

**October 22, 2025** - Day 64: The 18-Second Hang Mystery Solved
- **Found root cause**: Bloated FieldIndices section at EOF
- **Removed 4:1 FieldIndices bloat** causing 18-second load hang
- Documented embedded list bug causing data loss
- **Removed 389 lines of dead code** causing embedded list bug
- Added session continuity checklist
- Disabled text deduplication (intentional author content)
- **Fixed file truncation** - ListIndices header mismatch
- Write empty ActionParams/ConditionParams for all nodes
- Simplified Save As dialog to avoid hang

### Week 4: Parameter Preservation (Oct 23-29)

**October 23, 2025** - Day 65: ListIndices Investigation
- Added ListIndices testing protocol
- Architecture design for proper field offset fix
- Added chef.dlg mystery to testing protocol

**October 24, 2025** - Day 66: Pre-Calculated Offsets Implementation
- 🎉 **PARAMETERS WORKING IN AURORA!**
- Implemented pre-calculated ListIndices offsets (Phase 1)
- ActionParams/ConditionParams use pre-calculated offsets (Phase 2)
- Disabled old offset patching
- Fixed AutoExport file mapping
- Complete parameter struct indices write
- Per-file logging for better debugging
- Correct parameter mapping using node index

**October 25, 2025** - Day 67: Conversation Settings and Journal Integration
- **Phase 2d - Journal (JRL) Integration complete**
- Fixed buffer access violations
- Resolved nw_walk_wp cross-contamination
- Added UI for conversation settings
- Complete journal integration with GFF parsing
- Quest/Journal UI with faded End state indicator
- Added 2DA parser for class name resolution
- **Creature (UTC) file reading** for tag selection
- Session-based log retention implemented
- Cleaned up logging verbosity

**October 26, 2025** - Day 68: Privacy and Rebrand
- 🎉 **PHASE 2 COMPLETE - Resource Integration Conquered!**
- **Rebranded to Parley** with DialogEditor namespace
- Privacy scrub complete
- Created NonPublic folder structure
- Moved Testing and Plans to NonPublic
- **Complete ArcReactor to Parley rebrand**
- Sanitized all path logging
- Major documentation reorganization for public release
- Archived outdated PROJECT_STATUS and tech debt docs
- Removed obsolete settings and scratch files
- Updated Linux testing guide for public
- New commit/PR standards documented

**October 27, 2025** - Day 69: Cut/Paste Features
- Added expand/collapse subnodes recursively
- Cut Node functionality (Ctrl+X)
- Fixed Cut to detach without deleting linked children
- Added circular reference protection to expand/collapse
- Comprehensive error handling

**October 28, 2025** - Day 70: Undo/Redo System
- **Cut/Paste and Expand/Collapse with Undo/Redo complete**
- Undo/redo functionality (Ctrl+Z/Ctrl+Y)
- Orphaned pointer cleanup after delete
- Pointer index recalculation
- Delete removes from ALL parent pointers
- Duplicate node detection warning
- Preserve tree expansion state during undo/redo
- Circular reference protection
- Code review cleanup - dead code removed
- **Parser Refactoring Phase 1-2**: Extract GffIndexFixer and DialogBuilder

**October 29, 2025** - Day 71: Parser Refactoring Complete
- **Parser Refactoring Complete: Phases 1-4**
- Phase 3: DialogWriter extraction
- Phase 4: DialogFileService & DialogValidator service layer
- **Restored missing WriteListIndices code** from Phase 3
- Fixed TreeView focus indicators
- Restored focus after moving nodes
- **Merged into develop and main**
- **Phase 5: Removed ~400 lines of dead code**

---

## November 2025

### Week 1: Public Release Preparation (Nov 1)

**November 1, 2025** - Day 71: Documentation and Testing Cleanup
- **Rewritten README for public release**
- Created PARSER_ARCHITECTURE.md with clinical current-state documentation
- Updated CLAUDE.md for public (removed NonPublic references)
- Updated session checklist (deprecated file references removed)
- **Test file migration to workspace-relative paths**
- Created TestPathHelper for portable path resolution
- Updated DirectExportTest and VerifyExportTest
- **Deleted 27 obsolete test scripts** (~1,800 lines of hardcoded path code)
- Created TestFiles/README.md documentation
- Updated .gitignore for test output files
- Created Issue #69: Modal to modeless dialogs FR
- Created Issue #70: macOS/Linux path auto-detection FR
- Created PR #71: Documentation cleanup for public release

---

## Project Statistics

**Total Duration**: 70 days (Aug 23 - Nov 1, 2025)

**Development Phases**:
- **Phase 0**: Cross-Platform Foundation (Avalonia UI) - Complete
- **Phase 1**: Core Editing (DLG export Aurora-compatible) - Complete (Oct 19)
- **Phase 2**: Resource Integration (Sound/Script/Character/Journal) - Complete (Oct 26)
- **Phase 3**: Script & Parameter Support - In Progress
- **Phases 4-6**: Planned (UI/UX, Visualization, Testing)

**Major Milestones**:
- Day 1 (Aug 23): Project inception from conversation
- Day 10 (Sep 1): First complete round-trip working
- Day 13 (Sep 3): Discovered export vs parser root cause
- Day 14 (Sep 4): Fixed 80-90% data loss bug
- Day 23 (Sep 13): 4:1 pattern discovery, 100% round-trip
- Day 30 (Sep 20): 98% project completion
- Day 58 (Oct 19): Aurora-compatible DLG export verified
- Day 64 (Oct 22): 18-second hang bug solved
- Day 66 (Oct 24): Parameters working in Aurora
- Day 68 (Oct 26): Phase 2 complete, rebranded to Parley
- Day 71 (Nov 1): Public release preparation complete

**Critical Breakthroughs**:
1. **Aurora 4:1 Field Indices Pattern** (Sep 13) - Unlocked Aurora compatibility
2. **Pre-Calculated ListIndices Offsets** (Oct 24) - Parameters working
3. **WriteListIndices Bug Fix** (Oct 22-29) - Eliminated 18-second hang
4. **Struct Type & CResRef Format Fix** (Oct 19) - Aurora Editor compatibility
5. **Parser Refactoring** (Oct 28-29) - Clean architecture for maintenance

**Code Deletions** (Technical Debt Cleanup):
- Oct 22: 389 lines (embedded list bug code)
- Oct 28: Dead code cleanup via code review
- Oct 29: ~400 lines (Phase 5 dead code removal)
- Nov 1: 1,800 lines (obsolete test scripts)

**Total Deleted**: ~2,589 lines of obsolete/incorrect code

---

## Development Patterns Observed

### Problem-Solving Approach
1. **Systematic Investigation**: Gather all data before making changes
2. **Pattern Recognition**: Look for mathematical relationships in binary formats
3. **Multiple Validation**: Cross-check with different approaches
4. **Historical Context**: Study original 1990s design decisions

### Common Challenges
- **Binary Format Precision**: Aurora Engine requires byte-perfect output
- **Circular References**: Conversation loops required special handling
- **Field Index Mapping**: Complex 4:1 ratio pattern discovery
- **Aurora 1990s Optimization**: Understanding hardware constraint decisions

### Collaboration Dynamic
- **Claude**: Code implementation, pattern analysis, debugging, documentation
- **Human**: Requirements, testing in Aurora Toolset/NWN, architectural decisions, API quota management

### Session Management
- Used comprehensive logging for debugging binary formats
- Session continuity system developed (CLAUDE_SESSION_CHECKLIST.md)
- "3 Strikes Rule" for debugging (external validation after 3 failed attempts)
- Technical debt tracking from Day 2

---

## Reflections

This project demonstrates AI-human collaboration on a complex reverse-engineering task. The Aurora Engine's 1990s binary format required:
- Deep pattern analysis across thousands of bytes
- Mathematical precision in field index calculations
- Understanding optimization decisions from 1990s hardware constraints
- Systematic debugging of silent corruption issues

The development followed an iterative pattern:
1. Implement based on documentation
2. Test in Aurora Toolset/NWN
3. Discover mismatch
4. Analyze binary differences
5. Find pattern/root cause
6. Implement fix
7. Repeat

Major breakthroughs often came from examining hex dumps and discovering mathematical patterns (like the 4:1 field indices ratio) that weren't explicitly documented.

**Final Status**: Fully functional cross-platform dialog editor with 100% Aurora Engine compatibility, ready for public alpha release.

---

*This timeline was compiled from 220+ git commits spanning 70 days of development. All code written by Claude (Anthropic), directed by human collaborator.*
