namespace Tweaker.Core.Features.Common.Enums;

/// <summary>
/// What a Builder-mode session is authoring. Only meaningful when
/// <see cref="TweakerMode.Builder"/> is active.
/// </summary>
public enum BuilderTarget
{
    Config,
    Autounattend,
}
