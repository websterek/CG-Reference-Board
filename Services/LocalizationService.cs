using System;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class LocalizationService : ILocalizationService
{
    public string Get(string key)
    {
        return key;
    }

    public event EventHandler? CultureChanged;
}