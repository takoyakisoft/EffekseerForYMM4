using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using EffekseerForYMM4;

namespace EffekseerForYMM4.EffekseerAudioEffect
{
    /// <summary>
    /// 音声エフェクト
    /// 音声エフェクトには必ず[AudioEffect]属性を設定してください。
    /// </summary>
    [AudioEffect(nameof(Translate.Plugin_AudioEffect_Name), ["エフェクト"], ["Effekseer"], ResourceType = typeof(Translate))]
    public class EffekseerAudioEffect : AudioEffectBase
    {
        /// <summary>
        /// エフェクトの名前
        /// </summary>
        public override string Label => Translate.Plugin_AudioEffect_Name;

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Common_File_Name), Description = nameof(Translate.Common_File_Desc), ResourceType = typeof(Translate))]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        string filePath = "";

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Audio_Volume_Name), Description = nameof(Translate.Audio_Volume_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Volume { get; } = new Animation(100, 0, 1000);

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Common_Loop_Name), Description = nameof(Translate.Audio_Loop_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool IsLoop { get => isLoop; set => Set(ref isLoop, value); }
        bool isLoop = true;

        [Display(GroupName = nameof(Translate.Group_Camera), Name = nameof(Translate.Camera_X_Name), Description = nameof(Translate.Camera_X_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosX { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Camera), Name = nameof(Translate.Camera_Y_Name), Description = nameof(Translate.Camera_Y_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosY { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Camera), Name = nameof(Translate.Camera_Z_Name), Description = nameof(Translate.Camera_Z_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosZ { get; } = new Animation(20, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionX_Name), Description = nameof(Translate.Transform_PositionX_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosX { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionY_Name), Description = nameof(Translate.Transform_PositionY_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosY { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionZ_Name), Description = nameof(Translate.Transform_PositionZ_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosZ { get; } = new Animation(0, -100000.0, 100000.0);

        /// <summary>
        /// 音声エフェクトを作成する
        /// </summary>
        /// <param name="duration">音声エフェクトの長さ</param>
        /// <returns>音声エフェクト</returns>
        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new EffekseerAudioEffectProcessor(this, duration);
        }

        /// <summary>
        /// ExoFilterを作成する
        /// </summary>
        /// <param name="keyFrameIndex">キーフレーム番号</param>
        /// <param name="exoOutputDescription">exo出力に必要な各種項目</param>
        /// <returns>exoフィルタ</returns>
        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtlに音量を設定するためのフィルタが存在しないため、以下のフィルタは正常に機能しません。例示用です。
            var fps = exoOutputDescription.VideoInfo.FPS;
            return
            [
                $"_name=音量\r\n" +
                $"_disable={(IsEnabled ?1:0)}\r\n" +
                $"音量={Volume.ToExoString(keyFrameIndex, "F1", fps)}\r\n"
            ];
        }

        /// <summary>
        /// IAnimatableを実装するプロパティを返す
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Volume, CamPosX, CamPosY, CamPosZ, PosX, PosY, PosZ];
    }
}
