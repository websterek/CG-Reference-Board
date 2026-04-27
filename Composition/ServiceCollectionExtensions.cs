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
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IViewportService, ViewportService>();

        services.AddSingleton<ModeService>();
        services.AddSingleton<SelectionService>();
        services.AddSingleton<TransformService>();

        services.AddTransient<MainWindowViewModel>();

        services.AddTransient<MainWindow>();

        return services;
    }
}