using System;

namespace CGReferenceBoard.Services.Abstractions;

public interface ILocalizationService
{
    string Get(string key);
    string this[string key] { get; }
    event EventHandler? CultureChanged;
}