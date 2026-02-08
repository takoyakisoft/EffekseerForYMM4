using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4.EffekseerAudioEffect
{
    /// <summary>
    /// 音声エフェクト
    /// 音声エフェクトには必ず[AudioEffect]属性を設定してください。
    /// </summary>
    [AudioEffect("Effekseer音声エフェクト", ["Effekseer"], [])]
    public class EffekseerAudioEffect : AudioEffectBase
    {
        /// <summary>
        /// エフェクトの名前
        /// </summary>
        public override string Label => "Effekseer音声エフェクト";

        [Display(GroupName = "エフェクト", Name = "ファイル", Description = "エフェクトファイル")]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        string filePath = "";

        [Display(GroupName = "エフェクト", Name = "音量", Description = "音量を調整します")]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Volume { get; } = new Animation(100, 0, 1000);

        [Display(GroupName = "エフェクト", Name = "ループ", Description = "エフェクトをループ再生します")]
        [ToggleSlider]
        public bool IsLoop { get => isLoop; set => Set(ref isLoop, value); }
        bool isLoop = true;

        [Display(GroupName = "カメラ位置", Name = "X", Description = "カメラのX座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosX { get; } = new Animation(0, -10000, 10000);

        [Display(GroupName = "カメラ位置", Name = "Y", Description = "カメラのY座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosY { get; } = new Animation(0, -10000, 10000);

        [Display(GroupName = "カメラ位置", Name = "Z", Description = "カメラのZ座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosZ { get; } = new Animation(20, -10000, 10000);

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
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Volume, CamPosX, CamPosY, CamPosZ];
    }
}