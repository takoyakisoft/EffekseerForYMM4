using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4;

[AudioEffect(
    nameof(Translate.Plugin_AudioEffect_Name),
    ["エフェクト"],
    ["Effekseer"],
    ResourceType = typeof(Translate))]
public sealed class EffekseerAudioEffect : AudioEffectBase
{
    public override string Label => Translate.Plugin_AudioEffect_Name;

    [Display(
        GroupName = nameof(Translate.Group_Effect),
        Name = nameof(Translate.Common_File_Name),
        Description = nameof(Translate.Common_File_Desc),
        ResourceType = typeof(Translate))]
    [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
    public string FilePath
    {
        get => filePath;
        set => Set(ref filePath, value);
    }
    private string filePath = "";

    [Display(
        GroupName = nameof(Translate.Group_Effect),
        Name = nameof(Translate.Audio_Volume_Name),
        Description = nameof(Translate.Audio_Volume_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F0", "%", 0, 100)]
    public Animation Volume { get; } = new(
        100,
        EffekseerParameterSettings.PositiveAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Effect),
        Name = nameof(Translate.Common_Loop_Name),
        Description = nameof(Translate.Audio_Loop_Desc),
        ResourceType = typeof(Translate))]
    [ToggleSlider]
    public bool IsLoop
    {
        get => isLoop;
        set => Set(ref isLoop, value);
    }
    private bool isLoop = true;

    [Display(
        GroupName = nameof(Translate.Group_Camera),
        Name = nameof(Translate.Camera_X_Name),
        Description = nameof(Translate.Camera_X_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation CamPosX { get; } = new(
        0,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Camera),
        Name = nameof(Translate.Camera_Y_Name),
        Description = nameof(Translate.Camera_Y_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation CamPosY { get; } = new(
        0,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Camera),
        Name = nameof(Translate.Camera_Z_Name),
        Description = nameof(Translate.Camera_Z_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation CamPosZ { get; } = new(
        20,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Transform),
        Name = nameof(Translate.Transform_PositionX_Name),
        Description = nameof(Translate.Transform_PositionX_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation PosX { get; } = new(
        0,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Transform),
        Name = nameof(Translate.Transform_PositionY_Name),
        Description = nameof(Translate.Transform_PositionY_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation PosY { get; } = new(
        0,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    [Display(
        GroupName = nameof(Translate.Group_Transform),
        Name = nameof(Translate.Transform_PositionZ_Name),
        Description = nameof(Translate.Transform_PositionZ_Desc),
        ResourceType = typeof(Translate))]
    [AnimationSlider("F1", "px", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]
    public Animation PosZ { get; } = new(
        0,
        EffekseerParameterSettings.SignedAnimationMinimum,
        EffekseerParameterSettings.AnimationMaximum);

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration) =>
        new EffekseerAudioEffectProcessor(this, duration);

    public override IEnumerable<string> CreateExoAudioFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [Volume, CamPosX, CamPosY, CamPosZ, PosX, PosY, PosZ];
}
