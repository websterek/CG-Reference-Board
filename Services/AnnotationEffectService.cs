using System;
using CGReferenceBoard.Controls;

namespace CGReferenceBoard.Services;

public class AnnotationEffectService : Services.Abstractions.IAnnotationEffectService
{
    private AnnotationEffect _currentEffect = AnnotationEffect.None;

    public AnnotationEffect CurrentEffect => _currentEffect;

    public event Action? EffectModeChanged;

    public void SetEffectMode(AnnotationEffect mode)
    {
        if (_currentEffect == mode)
            return;
        _currentEffect = mode;
        EffectModeChanged?.Invoke();
        AnnotationShape.SetEffectMode(mode);
    }
}