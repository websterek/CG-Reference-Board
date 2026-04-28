using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CGReferenceBoard.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CGReferenceBoard.Markup;

public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localizationService = App.Services?.GetService<ILocalizationService>();
        if (localizationService is null)
            return Key;

        return new Binding($"[{Key}]")
        {
            Source = localizationService,
            Mode = BindingMode.OneWay
        };
    }
}
