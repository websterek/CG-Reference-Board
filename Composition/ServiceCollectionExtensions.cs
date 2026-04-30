using System;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.Modes;
using CGReferenceBoard.ViewModels;
using CGReferenceBoard.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CGReferenceBoard.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCgReferenceBoard(this IServiceCollection services)
    {
        services.AddSingleton<IBoardService, BoardService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IImageService, ImageServiceImpl>();
        services.AddSingleton<IViewportService, ViewportService>();
        services.AddSingleton<IBoardMigrationRegistry, BoardMigrationRegistry>();

        services.AddSingleton<ModeService>();
        services.AddSingleton<SelectionService>();
        services.AddSingleton<TransformService>();
        services.AddSingleton<Services.Abstractions.IAnnotationEffectService, AnnotationEffectService>();
        services.AddSingleton<IWindowChromeService>(new NullWindowChromeService());

        services.AddTransient<MainWindowViewModel>();

        services.AddTransient<IDropImportService>(sp =>
            new DropImportService(
                sp.GetRequiredService<MainWindowViewModel>(),
                sp.GetRequiredService<INotificationService>()));

        services.AddTransient<MainWindow>();

        return services;
    }
}