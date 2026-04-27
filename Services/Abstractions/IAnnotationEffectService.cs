using System;
using CGReferenceBoard.Controls;

namespace CGReferenceBoard.Services.Abstractions;

public interface IAnnotationEffectService
{
    AnnotationEffect CurrentEffect { get; }
    event Action? EffectModeChanged;
    void SetEffectMode(AnnotationEffect mode);
}

public static class AnnotationEffectServiceExtensions
{
    public static void SetEffectMode(this IAnnotationEffectService service, string modeName)
    {
        if (Enum.TryParse<AnnotationEffect>(modeName, out var mode))
            service.SetEffectMode(mode);
        else
            service.SetEffectMode(AnnotationEffect.None);
    }
}