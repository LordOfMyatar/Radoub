using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Views;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using DialogEditor.Utils;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Populates speaker, portrait, and soundset properties in the Properties Panel.
    /// Extracted from PropertyPanelPopulator to reduce class size (Epic #1219, Sprint 2.1 #1226).
    ///
    /// Handles:
    /// 1. Speaker field population and read-only state for PC nodes
    /// 2. Speaker visual preferences (shape/color ComboBoxes)
    /// 3. Soundset info display and preview controls
    /// 4. Portrait loading from BIF archives via ImageService
    /// </summary>
    public class SpeakerPropertiesPopulator
    {
        private readonly Window _window;
        private readonly ISettingsService _settings;
        private IImageService? _imageService;
        private IGameDataService? _gameDataService;

        /// <summary>
        /// Callback to set the current soundset ID in MainWindow for play button (#916).
        /// </summary>
        public Action<ushort>? SetCurrentSoundsetId { get; set; }

        public SpeakerPropertiesPopulator(Window window, ISettingsService settings)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Sets the image service for loading portraits from BIF archives (#916).
        /// </summary>
        public void SetImageService(IImageService imageService)
        {
            _imageService = imageService;
        }

        /// <summary>
        /// Sets the game data service for 2DA lookups from BIF archives (#916).
        /// </summary>
        public void SetGameDataService(IGameDataService gameDataService)
        {
            _gameDataService = gameDataService;
        }

        /// <summary>
        /// Populates speaker field and related controls.
        /// PC nodes have read-only speaker field.
        /// </summary>
        public void PopulateSpeaker(DialogNode dialogNode, CreatureService? creatureService = null)
        {
            var speakerTextBox = _window.FindControl<TextBox>("SpeakerTextBox");
            var recentCreatureComboBox = _window.FindControl<ComboBox>("RecentCreatureTagsComboBox");
            var browseCreatureButton = _window.FindControl<Button>("BrowseCreatureButton");

            bool isPC = (dialogNode.Type == DialogNodeType.Reply);

            if (speakerTextBox != null)
            {
                speakerTextBox.Text = dialogNode.Speaker ?? "";
                speakerTextBox.IsReadOnly = isPC;

                if (isPC)
                {
                    speakerTextBox.Watermark = "PC (player character)";
                }
                else
                {
                    speakerTextBox.Watermark = "Character tag or empty for Owner";
                }
            }

            // Disable Speaker dropdown and Browse button for PC nodes
            if (recentCreatureComboBox != null)
            {
                recentCreatureComboBox.IsEnabled = !isPC;
            }

            if (browseCreatureButton != null)
            {
                browseCreatureButton.IsEnabled = !isPC;
            }

            // Populate NPC speaker visual preferences (Issue #16, #36)
            PopulateSpeakerVisualPreferences(dialogNode);

            // Populate soundset info (Issue #786)
            PopulateSoundsetInfo(dialogNode, isPC, creatureService);
        }

        /// <summary>
        /// Populates shape/color ComboBoxes based on speaker tag preferences.
        /// Disabled for PC nodes and empty speakers (Owner).
        /// </summary>
        public void PopulateSpeakerVisualPreferences(DialogNode dialogNode)
        {
            var shapeComboBox = _window.FindControl<ComboBox>("SpeakerShapeComboBox");
            var colorComboBox = _window.FindControl<ComboBox>("SpeakerColorComboBox");

            bool isPC = (dialogNode.Type == DialogNodeType.Reply);
            bool hasNamedSpeaker = !string.IsNullOrWhiteSpace(dialogNode.Speaker);

            // Disable for PC nodes and Owner (empty speaker)
            bool enableControls = !isPC && hasNamedSpeaker;

            if (shapeComboBox != null)
            {
                shapeComboBox.IsEnabled = enableControls;
                if (enableControls)
                {
                    var (_, prefShape) = _settings.GetSpeakerPreference(dialogNode.Speaker);
                    if (prefShape.HasValue)
                    {
                        // Set to preference
                        shapeComboBox.SelectedItem = prefShape.Value;
                    }
                    else
                    {
                        // Show hash-based default
                        var defaultShape = SpeakerVisualHelper.GetSpeakerShape(dialogNode.Speaker, false);
                        shapeComboBox.SelectedItem = defaultShape;
                    }
                }
                else
                {
                    shapeComboBox.SelectedIndex = -1;
                }
            }

            if (colorComboBox != null)
            {
                colorComboBox.IsEnabled = enableControls;
                if (enableControls)
                {
                    var (prefColor, _) = _settings.GetSpeakerPreference(dialogNode.Speaker);
                    if (!string.IsNullOrEmpty(prefColor))
                    {
                        // Set to preference
                        foreach (var obj in colorComboBox.Items)
                        {
                            if (obj is ComboBoxItem item && item.Tag as string == prefColor)
                            {
                                colorComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Show hash-based default
                        var defaultColor = SpeakerVisualHelper.GetSpeakerColor(dialogNode.Speaker, false);
                        foreach (var obj in colorComboBox.Items)
                        {
                            if (obj is ComboBoxItem item && item.Tag as string == defaultColor)
                            {
                                colorComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    colorComboBox.SelectedIndex = -1;
                }
            }
        }

        /// <summary>
        /// Clears speaker-related fields (soundset info and portrait).
        /// Called from PropertyPanelPopulator.ClearAllFields().
        /// </summary>
        public void ClearSpeakerFields()
        {
            // Issue #786, #915: Clear soundset info and portrait
            var soundsetInfoTextBlock = _window.FindControl<TextBlock>("SoundsetInfoTextBlock");
            if (soundsetInfoTextBlock != null)
                soundsetInfoTextBlock.Text = "";

            var portraitBorder = _window.FindControl<Border>("PortraitBorder");
            if (portraitBorder != null)
                portraitBorder.IsVisible = false;
        }

        /// <summary>
        /// Populates creature info from tag lookup (#786 soundset, #915 portrait).
        /// Shows portrait image and soundset info for NPC speakers with creatures.
        /// </summary>
        private void PopulateSoundsetInfo(DialogNode dialogNode, bool isPC, CreatureService? creatureService)
        {
            var soundsetInfoTextBlock = _window.FindControl<TextBlock>("SoundsetInfoTextBlock");
            var portraitBorder = _window.FindControl<Border>("PortraitBorder");
            var portraitImage = _window.FindControl<Image>("PortraitImage");

            // Get soundset preview panel for visibility management
            var soundsetPreviewPanel = _window.FindControl<StackPanel>("SoundsetPreviewPanel");

            // Clear portrait and soundset for PC nodes or empty speaker
            if (isPC || string.IsNullOrWhiteSpace(dialogNode.Speaker))
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = "";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                if (soundsetPreviewPanel != null)
                    soundsetPreviewPanel.IsVisible = false;
                // Reset soundset ID to prevent stale state (#1006)
                SetCurrentSoundsetId?.Invoke(ushort.MaxValue);
                return;
            }

            // Try to look up creature by speaker tag
            if (creatureService == null || !creatureService.HasCachedCreatures)
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = "";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                if (soundsetPreviewPanel != null)
                    soundsetPreviewPanel.IsVisible = false;
                // Reset soundset ID to prevent stale state (#1006)
                SetCurrentSoundsetId?.Invoke(ushort.MaxValue);
                return;
            }

            var creature = creatureService.GetCreatureByTag(dialogNode.Speaker);
            if (creature == null)
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = $"Creature '{dialogNode.Speaker}' not found in module";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                if (soundsetPreviewPanel != null)
                    soundsetPreviewPanel.IsVisible = false;
                // Reset soundset ID to prevent stale state (#1006)
                SetCurrentSoundsetId?.Invoke(ushort.MaxValue);
                return;
            }

            // Load and display portrait image (#915, #916)
            if (portraitBorder != null && portraitImage != null)
            {
                Bitmap? portrait = null;
                string? portraitResRef = creature.PortraitResRef;

                // If creature doesn't have a resolved PortraitResRef, look it up via GameDataService
                // This happens when portraits.2da is only available in BIF archives (NWN:EE)
                if (string.IsNullOrEmpty(portraitResRef) && creature.PortraitId > 0 && _gameDataService != null)
                {
                    portraitResRef = _gameDataService.Get2DAValue("portraits", creature.PortraitId, "BaseResRef");
                    if (!string.IsNullOrEmpty(portraitResRef))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Resolved portrait ID {creature.PortraitId} to ResRef '{portraitResRef}' via GameDataService");
                    }
                }

                // Try to load portrait by ResRef using ImageService for BIF lookup
                if (!string.IsNullOrEmpty(portraitResRef) && _imageService != null)
                {
                    var imageData = _imageService.GetPortrait(portraitResRef);
                    if (imageData != null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Portrait imageData for {creature.Tag}: {imageData.Width}x{imageData.Height}");
                        portrait = ImageDataToBitmap(imageData);
                    }
                }

                if (portrait != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Portrait bitmap for {creature.Tag}: {portrait.PixelSize.Width}x{portrait.PixelSize.Height}");
                    portraitImage.Source = portrait;
                    portraitBorder.IsVisible = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded portrait for {creature.Tag}: {portraitResRef}");
                }
                else
                {
                    portraitBorder.IsVisible = false;
                    if (!string.IsNullOrEmpty(portraitResRef))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait not found for {creature.Tag}: {portraitResRef}");
                    }
                    else if (creature.PortraitId > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Could not resolve portrait ID {creature.PortraitId} for {creature.Tag}");
                    }
                }
            }

            // Build info string with soundset (#786)
            var infoParts = new List<string>();

            // Show creature name
            if (!string.IsNullOrEmpty(creature.DisplayName))
            {
                infoParts.Add(creature.DisplayName);
            }

            // Soundset info (#786, #916)
            if (!string.IsNullOrEmpty(creature.SoundSetSummary))
            {
                infoParts.Add($"Soundset: {creature.SoundSetSummary}");
            }
            else if (creature.SoundSetFile != ushort.MaxValue && _gameDataService != null)
            {
                // Try to look up soundset name via GameDataService (BIF support)
                var strRefStr = _gameDataService.Get2DAValue("soundset", creature.SoundSetFile, "STRREF");
                var genderStr = _gameDataService.Get2DAValue("soundset", creature.SoundSetFile, "GENDER");
                string? soundsetName = null;

                if (uint.TryParse(strRefStr, out var strRef) && strRef < 0x1000000)
                {
                    soundsetName = _gameDataService.GetString(strRef);
                }

                if (!string.IsNullOrEmpty(soundsetName))
                {
                    var gender = genderStr == "1" ? "Female" : "Male";
                    infoParts.Add($"Soundset: {soundsetName} ({gender})");
                }
                else
                {
                    // Fallback to ID if name lookup fails
                    infoParts.Add($"Soundset ID: {creature.SoundSetFile}");
                }
            }
            else if (creature.SoundSetFile != ushort.MaxValue)
            {
                infoParts.Add($"Soundset ID: {creature.SoundSetFile}");
            }

            // Display combined info
            if (soundsetInfoTextBlock != null)
            {
                if (infoParts.Count > 0)
                {
                    soundsetInfoTextBlock.Text = string.Join(" | ", infoParts);
                }
                else
                {
                    soundsetInfoTextBlock.Text = $"Tag: {creature.Tag}";
                }
            }

            // Setup soundset preview controls (#916)
            var soundsetTypeCombo = _window.FindControl<ComboBox>("SoundsetTypeComboBox");

            if (soundsetPreviewPanel != null && soundsetTypeCombo != null)
            {
                bool hasSoundset = creature.SoundSetFile != ushort.MaxValue;
                soundsetPreviewPanel.IsVisible = hasSoundset;

                if (hasSoundset)
                {
                    // Store soundset ID for play handler
                    SetCurrentSoundsetId?.Invoke(creature.SoundSetFile);

                    // Populate combo if empty
                    if (soundsetTypeCombo.ItemCount == 0)
                    {
                        soundsetTypeCombo.ItemsSource = GetSoundsetTypeItems();
                        soundsetTypeCombo.SelectedIndex = 0; // Hello
                    }
                }
            }
        }

        /// <summary>
        /// Gets the common sound type items for the dropdown (#916).
        /// </summary>
        private static List<SoundsetTypeItem> GetSoundsetTypeItems()
        {
            return new List<SoundsetTypeItem>
            {
                new() { Name = "Hello", SoundType = Radoub.Formats.Ssf.SsfSoundType.Hello },
                new() { Name = "Goodbye", SoundType = Radoub.Formats.Ssf.SsfSoundType.Goodbye },
                new() { Name = "Yes", SoundType = Radoub.Formats.Ssf.SsfSoundType.Yes },
                new() { Name = "No", SoundType = Radoub.Formats.Ssf.SsfSoundType.No },
                new() { Name = "Attack", SoundType = Radoub.Formats.Ssf.SsfSoundType.Attack },
                new() { Name = "Battlecry", SoundType = Radoub.Formats.Ssf.SsfSoundType.Battlecry1 },
                new() { Name = "Taunt", SoundType = Radoub.Formats.Ssf.SsfSoundType.Taunt },
                new() { Name = "Death", SoundType = Radoub.Formats.Ssf.SsfSoundType.Death },
                new() { Name = "Laugh", SoundType = Radoub.Formats.Ssf.SsfSoundType.Laugh },
                new() { Name = "Selected", SoundType = Radoub.Formats.Ssf.SsfSoundType.Selected },
            };
        }

        /// <summary>
        /// Converts ImageData from Radoub.Formats to an Avalonia Bitmap (#916).
        /// Uses SkiaSharp for reliable image conversion.
        /// </summary>
        private static Bitmap? ImageDataToBitmap(ImageData imageData)
        {
            if (imageData.Width == 0 || imageData.Height == 0 || imageData.Pixels == null)
                return null;

            try
            {
                int expectedSize = imageData.Width * imageData.Height * 4;
                if (imageData.Pixels.Length != expectedSize)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"ImageDataToBitmap: Invalid pixel data - expected {expectedSize}, got {imageData.Pixels.Length}");
                    return null;
                }

                // Create SkiaSharp bitmap from RGBA data
                var info = new SkiaSharp.SKImageInfo(imageData.Width, imageData.Height,
                    SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                using var skBitmap = new SkiaSharp.SKBitmap(info);

                // Copy pixel data
                var pixels = skBitmap.GetPixels();
                if (pixels == IntPtr.Zero)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "ImageDataToBitmap: GetPixels returned null");
                    return null;
                }

                System.Runtime.InteropServices.Marshal.Copy(imageData.Pixels, 0, pixels, imageData.Pixels.Length);

                // Encode to PNG in memory
                using var image = SkiaSharp.SKImage.FromBitmap(skBitmap);
                if (image == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "ImageDataToBitmap: SKImage.FromBitmap returned null");
                    return null;
                }

                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                if (data == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "ImageDataToBitmap: Encode returned null");
                    return null;
                }

                using var stream = new MemoryStream(data.ToArray());
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to convert image data to bitmap: {ex.Message}");
                return null;
            }
        }
    }
}
