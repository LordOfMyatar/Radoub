using System.Runtime.CompilerServices;
using DialogEditor;
using DialogEditor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Parley.Tests
{
    /// <summary>
    /// Initializes a minimal DI container for unit tests.
    /// #1233: Tests need Program.Services populated since services
    /// now resolve from DI instead of static .Instance properties.
    /// </summary>
    public static class TestServiceProvider
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // Skip if already initialized (e.g., by another test assembly)
            if (Program.Services != null)
                return;

            var services = new ServiceCollection();

            // Sub-services used internally by SettingsService
            services.AddSingleton<RecentFilesService>();
            services.AddSingleton<UISettingsService>();
            services.AddSingleton<WindowLayoutService>();
            services.AddSingleton<SpeakerPreferencesService>();
            services.AddSingleton<ParameterCacheService>();

            // Core services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<DialogContextService>();
            services.AddSingleton<IDialogContextService>(sp => sp.GetRequiredService<DialogContextService>());
            services.AddSingleton<ScriptService>();
            services.AddSingleton<IScriptService>(sp => sp.GetRequiredService<ScriptService>());
            services.AddSingleton<PortraitService>();
            services.AddSingleton<IPortraitService>(sp => sp.GetRequiredService<PortraitService>());
            services.AddSingleton<JournalService>();
            services.AddSingleton<IJournalService>(sp => sp.GetRequiredService<JournalService>());

            // Additional services
            services.AddSingleton<GameResourceService>();
            services.AddSingleton<ExternalEditorService>();
            services.AddSingleton<DialogEditor.Services.SpellCheckService>();
            services.AddSingleton<SoundCache>();
            services.AddSingleton<CoverageTracker>();
            services.AddSingleton<ITtsService>(sp => TtsServiceFactory.Create());

            Program.Services = services.BuildServiceProvider();
        }
    }
}
