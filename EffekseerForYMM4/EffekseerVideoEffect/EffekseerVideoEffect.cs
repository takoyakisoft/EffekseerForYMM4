using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{



    [VideoEffect(nameof(Translate.Plugin_VideoEffect_Name), ["装飾"], ["Effekseer"], ResourceType = typeof(Translate))]
    internal class EffekseerVideoEffect : VideoEffectBase
    {
        public override string Label => Name;

        /// <summary>
        /// プラグインの名前
        /// </summary>
        public string Name => Translate.Plugin_VideoEffect_Name;

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Common_File_Name), Description = nameof(Translate.Common_File_Desc), ResourceType = typeof(Translate))]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.None)]
        public string FilePath { get => filePath; set => Set(ref filePath, value); }
        string filePath = "";

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Video_ScreenSize_Name), Description = nameof(Translate.Video_ScreenSize_Desc), ResourceType = typeof(Translate))]
        [ToggleSlider]
        public bool IsScreenSize { get => isScreenSize; set => Set(ref isScreenSize, value); }
        bool isScreenSize = true;

        [Display(GroupName = nameof(Translate.Group_Effect), Name = nameof(Translate.Common_Loop_Name), Description = nameof(Translate.Video_Loop_Desc), ResourceType = typeof(Translate))]
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

        [Display(GroupName = nameof(Translate.Group_Camera), Name = nameof(Translate.Video_Fov_Name), Description = nameof(Translate.Video_Fov_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F0", "°", 1, 179)]
        public Animation Fov { get; } = new Animation(90, 1, 179);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_Scale_Name), Description = nameof(Translate.Transform_Scale_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Scale { get; } = new Animation(100.0, 0.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionX_Name), Description = nameof(Translate.Transform_PositionX_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosX { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionY_Name), Description = nameof(Translate.Transform_PositionY_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosY { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_PositionZ_Name), Description = nameof(Translate.Transform_PositionZ_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "m", -50, 50)]
        public Animation PosZ { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_RotationX_Name), Description = nameof(Translate.Transform_RotationX_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotX { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_RotationY_Name), Description = nameof(Translate.Transform_RotationY_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotY { get; } = new Animation(0, -100000.0, 100000.0);

        [Display(GroupName = nameof(Translate.Group_Transform), Name = nameof(Translate.Transform_RotationZ_Name), Description = nameof(Translate.Transform_RotationZ_Desc), ResourceType = typeof(Translate))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotZ { get; } = new Animation(0, -100000.0, 100000.0);

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
