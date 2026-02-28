using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{



    [VideoEffect("Effekseerビデオエフェクト", ["装飾"], ["Effekseer"])]
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

        [Display(GroupName = "カメラ", Name = "X", Description = "カメラのX座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosX { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "カメラ", Name = "Y", Description = "カメラのY座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosY { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "カメラ", Name = "Z", Description = "カメラのZ座標")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation CamPosZ { get; } = new Animation(20, -1000, 1000);

        [Display(GroupName = "カメラ", Name = "視野角", Description = "視野角(度)")]
        [AnimationSlider("F0", "°", 1, 179)]
        public Animation Fov { get; } = new Animation(90, 1, 179);

        [Display(GroupName = "変形", Name = "拡大率", Description = "エフェクト全体の拡大率")]
        [AnimationSlider("F2", "x", 0.01, 10)]
        public Animation Scale { get; } = new Animation(1.0, 0.01, 100.0);

        [Display(GroupName = "変形", Name = "位置X", Description = "エフェクト位置X")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosX { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "変形", Name = "位置Y", Description = "エフェクト位置Y")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosY { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "変形", Name = "位置Z", Description = "エフェクト位置Z")]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosZ { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = "変形", Name = "回転X", Description = "エフェクト回転X")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation RotX { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = "変形", Name = "回転Y", Description = "エフェクト回転Y")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation RotY { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = "変形", Name = "回転Z", Description = "エフェクト回転Z")]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation RotZ { get; } = new Animation(0, -3600, 3600);

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
        protected override IEnumerable<IAnimatable> GetAnimatables() => [CamPosX, CamPosY, CamPosZ, Fov, Scale, PosX, PosY, PosZ, RotX, RotY, RotZ];
    }
}
