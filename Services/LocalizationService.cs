using System;
using System.Globalization;
using System.Resources;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;

    public LocalizationService()
    {
        _resourceManager = new ResourceManager("CGReferenceBoard.Localization.Strings", typeof(LocalizationService).Assembly);
    }

    public string Get(string key)
    {
        return _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public string this[string key] => Get(key);

    public event EventHandler? CultureChanged;
}
