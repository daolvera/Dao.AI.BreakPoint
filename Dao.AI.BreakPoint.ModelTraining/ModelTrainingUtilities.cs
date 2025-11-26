using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

internal static class ModelTrainingUtilities
{
    internal static float[] CreateConcatenatedTarget(
        float overallScore,
        TechniqueIssues techniqueAnalysis,
        string[] issueCategories
    )
    {
        var target = new List<float>
        {
            // Overall score (1 value)
            overallScore,

            // Technique scores (5 values)
            techniqueAnalysis.ShoulderRotationScore,
            techniqueAnalysis.ContactPointScore,
            techniqueAnalysis.PreparationTimingScore,
            techniqueAnalysis.BalanceScore,
            techniqueAnalysis.FollowThroughScore
        };

        // Issue binary labels (N values)
        var issueLabels = CreateIssueBinaryLabels(
            techniqueAnalysis.DetectedIssues,
            issueCategories
        );
        target.AddRange(issueLabels);

        return [.. target];
    }

    internal static float[] CreateIssueBinaryLabels(string[] detectedIssues, string[] allCategories)
    {
        var labels = new float[allCategories.Length];
        for (int i = 0; i < allCategories.Length; i++)
        {
            labels[i] = detectedIssues.Contains(allCategories[i]) ? 1.0f : 0.0f;
        }
        return labels;
    }

    internal static Tensorflow.NumPy.NDArray ConvertToNumpyArray(this float[,,] array)
    {
        var shape = new[] { array.GetLength(0), array.GetLength(1), array.GetLength(2) };
        var flatArray = new float[array.GetLength(0) * array.GetLength(1) * array.GetLength(2)];

        int index = 0;
        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                for (int k = 0; k < array.GetLength(2); k++)
                {
                    flatArray[index++] = array[i, j, k];
                }
            }
        }

        return np.array(flatArray).reshape(shape);
    }

    internal static NDArray ConvertTargetsToNumpyArray(this float[][] targets)
    {
        int numSamples = targets.Length;
        int numFeatures = targets[0].Length;
        var flatArray = new float[numSamples * numFeatures];

        int index = 0;
        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numFeatures; j++)
            {
                flatArray[index++] = targets[i][j];
            }
        }

        return np.array(flatArray).reshape(new[] { numSamples, numFeatures });
    }
}
