using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{



    [VideoEffect("Effekseerビデオエフェクト", ["Effekseer"], [])]
    internal class EffekseerVideoEffect : VideoEffectBase
    {
        public override string Label => Name;

        /// <summary>
        /// プラグインの名前
        /// </summary>
        public string Name => "Effekseerビデオエフェクト";

        [Display(GroupName = "エフェクト", Name = "ファイル", Description = "エフェクトファイル")]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        string filePath = "";

        [Display(GroupName = "エフェクト", Name = "スクリーンサイズ", Description = "スクリーンサイズに合わせてレンダリングする")]
        [ToggleSlider]
        public bool IsScreenSize { get => isScreenSize; set => Set(ref isScreenSize, value); }
        bool isScreenSize = true;

        [Display(GroupName = "エフェクト", Name = "ループ", Description = "エフェクトをループ再生する")]
        [ToggleSlider]
        public bool IsLoop { get => isLoop; set => Set(ref isLoop, value); }
        bool isLoop = true;

        [Display(GroupName = "カメラ位置", Name = "X", Description = "カメラのX座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosX { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "カメラ位置", Name = "Y", Description = "カメラのY座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosY { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "カメラ位置", Name = "Z", Description = "カメラのZ座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosZ { get; } = new Animation(20, -1000, 1000);

        [Display(GroupName = "カメラ設定", Name = "視野角", Description = "視野角(度)")]
        [AnimationSlider("F0", "°", 1, 179)]
        public Animation Fov { get; } = new Animation(90, 1, 179);

        /// <summary>
        /// Exoフィルタを作成する。
        /// </summary>
        /// <param name="keyFrameIndex">キーフレーム番号</param>
        /// <param name="exoOutputDescription">exo出力に必要な各種情報</param>
        /// <returns></returns>
        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        /// <summary>
        /// 映像エフェクトを作成する
        /// </summary>
        /// <param name="devices">デバイス</param>
        /// <returns>映像エフェクト</returns>
        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new EffekseerVideoEffectProcessor(devices, this);
        }

        /// <summary>
        /// クラス内のIAnimatableを列挙する。
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<IAnimatable> GetAnimatables() => [CamPosX, CamPosY, CamPosZ, Fov];
    }
}
