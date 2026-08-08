using System.ComponentModel.DataAnnotations;

namespace EffekseerForYMM4;

public enum ProjectionMode
{
    [Display(Name = nameof(Translate.Video_Projection_Perspective), ResourceType = typeof(Translate))]
    Perspective,

    [Display(Name = nameof(Translate.Video_Projection_Orthographic), ResourceType = typeof(Translate))]
    Orthographic,
}
