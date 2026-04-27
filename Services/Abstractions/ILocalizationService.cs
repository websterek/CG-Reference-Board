using System;

namespace CGReferenceBoard.Services.Abstractions;

public interface ILocalizationService
{
    string Get(string key);
    event EventHandler? CultureChanged;
}