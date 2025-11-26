using Tensorflow.Keras.Engine;
using static Tensorflow.KerasApi;

namespace Dao.AI.BreakPoint.ModelTraining;

public static class SwingCnnModel
{
    public static IModel BuildSingleOutputModel(
        int sequenceLength,
        int numFeatures,
        int numIssueCategories
    )
    {
        var input = keras.Input(shape: (sequenceLength, numFeatures));

        // Shared CNN layers
        var conv1 = keras.layers.Conv1D(64, 3, activation: "relu", padding: "same").Apply(input);
        var pool1 = keras.layers.MaxPooling1D(2).Apply(conv1);
        var dropout1 = keras.layers.Dropout(0.3f).Apply(pool1);

        var conv2 = keras
            .layers.Conv1D(128, 5, activation: "relu", padding: "same")
            .Apply(dropout1);
        var pool2 = keras.layers.MaxPooling1D(2).Apply(conv2);
        var dropout2 = keras.layers.Dropout(0.3f).Apply(pool2);

        var conv3 = keras
            .layers.Conv1D(256, 7, activation: "relu", padding: "same")
            .Apply(dropout2);
        var globalPool = keras.layers.GlobalAveragePooling1D().Apply(conv3);
        var dropout3 = keras.layers.Dropout(0.4f).Apply(globalPool);

        // Dense shared features
        var dense = keras.layers.Dense(512, activation: "relu").Apply(dropout3);
        var dropout4 = keras.layers.Dropout(0.4f).Apply(dense);

        // Single concatenated output:
        // [overall_score, shoulder_score, contact_score, prep_score, balance_score, follow_score, issue1, issue2, ...]
        var totalOutputs = 1 + 5 + numIssueCategories; // 1 overall + 5 technique + N issues
        var output = keras.layers.Dense(totalOutputs, activation: "linear").Apply(dropout4);

        var model = keras.Model(inputs: input, outputs: output);
        return model;
    }

    public static void CompileModel(IModel model, float learningRate = 0.001f)
    {
        var optimizer = keras.optimizers.Adam(learning_rate: learningRate);

        model.compile(
            optimizer: optimizer,
            loss: keras.losses.MeanSquaredError(),
            metrics: ["mae"]
        );
    }
}
