namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingSequenceInput
{
    public required float[,] Sequences { get; set; }
    public required float[] Labels { get; set; }
    public required int[] SequenceLengths { get; set; }
}
