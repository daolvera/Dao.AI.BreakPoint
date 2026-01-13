using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Services.Options;

public class MoveNetOptions
{
    public const string SectionName = "MoveNet";
    [Required]
    public required string ModelPath { get; set; } = "models/movenet_output_model.onnx";
}
