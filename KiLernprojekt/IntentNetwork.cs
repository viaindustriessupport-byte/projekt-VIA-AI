using System.IO;
using System.Text.Json;

namespace KiLernprojekt;

public sealed class IntentNetwork
{
    private const int InputCount = 256;
    private const int Hidden1Count = 32;
    private const int Hidden2Count = 16;
    private const int ModelVersion = 2;
    private const double LearningRate = 0.04;

    private readonly Random random = new(42);
    private string[] labels;
    private double[,] inputWeights;
    private double[] hidden1Biases;
    private double[,] hiddenWeights;
    private double[] hiddenBiases;
    private double[,] outputWeights;
    private double[] outputBiases;

    public IntentNetwork(string[] labels)
    {
        if (labels.Length == 0 || labels.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Mindestens eine gültige Klasse ist erforderlich.", nameof(labels));
        }

        this.labels = [.. labels];
        inputWeights = new double[Hidden1Count, InputCount];
        hidden1Biases = new double[Hidden1Count];
        hiddenWeights = new double[Hidden2Count, Hidden1Count];
        hiddenBiases = new double[Hidden2Count];
        outputWeights = new double[labels.Length, Hidden2Count];
        outputBiases = new double[labels.Length];
        InitializeWeights();
    }

    public void Train(string text, string label, int epochs = 12)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epochs);

        int labelIndex = Array.IndexOf(labels, label);
        if (labelIndex < 0)
        {
            throw new ArgumentException($"Unbekannte Trainingsklasse: {label}", nameof(label));
        }

        double[] features = Features(text);
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            double[] hidden1 = Activate(inputWeights, hidden1Biases, features);
            double[] hidden2 = Activate(hiddenWeights, hiddenBiases, hidden1);
            double[] probabilities = Softmax(Score(hidden2));

            double[] outputDeltas = new double[labels.Length];
            for (int output = 0; output < labels.Length; output++)
            {
                outputDeltas[output] = probabilities[output] - (output == labelIndex ? 1.0 : 0.0);
            }

            double[] hidden2Deltas = new double[Hidden2Count];
            for (int hidden = 0; hidden < Hidden2Count; hidden++)
            {
                double error = 0;
                for (int output = 0; output < labels.Length; output++)
                {
                    error += outputWeights[output, hidden] * outputDeltas[output];
                }

                hidden2Deltas[hidden] = error * (1 - (hidden2[hidden] * hidden2[hidden]));
            }

            double[] hidden1Deltas = new double[Hidden1Count];
            for (int hidden = 0; hidden < Hidden1Count; hidden++)
            {
                double error = 0;
                for (int next = 0; next < Hidden2Count; next++)
                {
                    error += hiddenWeights[next, hidden] * hidden2Deltas[next];
                }

                hidden1Deltas[hidden] = error * (1 - (hidden1[hidden] * hidden1[hidden]));
            }

            Update(outputWeights, outputBiases, hidden2, outputDeltas);
            Update(hiddenWeights, hiddenBiases, hidden1, hidden2Deltas);
            Update(inputWeights, hidden1Biases, features, hidden1Deltas);
        }
    }

    public string Predict(string text)
    {
        double[] features = Features(text);
        double[] hidden1 = Activate(inputWeights, hidden1Biases, features);
        double[] hidden2 = Activate(hiddenWeights, hiddenBiases, hidden1);
        double[] scores = Score(hidden2);
        int bestLabel = Array.IndexOf(scores, scores.Max());
        return labels[bestLabel];
    }

    public void Save(string fileName)
    {
        var model = new
        {
            Version = ModelVersion,
            Labels = labels,
            InputCount,
            Hidden1Count,
            Hidden2Count,
            InputWeights = Flatten(inputWeights),
            Hidden1Biases = hidden1Biases,
            HiddenWeights = Flatten(hiddenWeights),
            HiddenBiases = hiddenBiases,
            OutputWeights = Flatten(outputWeights),
            OutputBiases = outputBiases,
        };

        File.WriteAllText(fileName, JsonSerializer.Serialize(model));
    }

    public bool Load(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fileName));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("Version", out JsonElement version) ||
            version.GetInt32() != ModelVersion ||
            !root.TryGetProperty("InputCount", out JsonElement inputCount) ||
            inputCount.GetInt32() != InputCount ||
            !root.TryGetProperty("Hidden1Count", out JsonElement hidden1Count) ||
            hidden1Count.GetInt32() != Hidden1Count ||
            !root.TryGetProperty("Hidden2Count", out JsonElement hidden2Count) ||
            hidden2Count.GetInt32() != Hidden2Count ||
            !root.TryGetProperty("Labels", out JsonElement labelsElement))
        {
            return false;
        }

        string[]? savedLabels = labelsElement.Deserialize<string[]>();
        if (savedLabels is null ||
            savedLabels.Length == 0 ||
            savedLabels.Any(string.IsNullOrWhiteSpace) ||
            savedLabels.Distinct(StringComparer.Ordinal).Count() != savedLabels.Length ||
            !TryReadValues(root, "InputWeights", Hidden1Count * InputCount, out double[] savedInputWeights) ||
            !TryReadValues(root, "Hidden1Biases", Hidden1Count, out double[] savedHidden1Biases) ||
            !TryReadValues(root, "HiddenWeights", Hidden2Count * Hidden1Count, out double[] savedHiddenWeights) ||
            !TryReadValues(root, "HiddenBiases", Hidden2Count, out double[] savedHiddenBiases) ||
            !TryReadValues(root, "OutputWeights", savedLabels.Length * Hidden2Count, out double[] savedOutputWeights) ||
            !TryReadValues(root, "OutputBiases", savedLabels.Length, out double[] savedOutputBiases))
        {
            return false;
        }

        labels = savedLabels;
        inputWeights = Expand(savedInputWeights, Hidden1Count, InputCount);
        hidden1Biases = savedHidden1Biases;
        hiddenWeights = Expand(savedHiddenWeights, Hidden2Count, Hidden1Count);
        hiddenBiases = savedHiddenBiases;
        outputWeights = Expand(savedOutputWeights, labels.Length, Hidden2Count);
        outputBiases = savedOutputBiases;
        return true;
    }

    private void InitializeWeights()
    {
        Initialize(inputWeights, InputCount, Hidden1Count);
        Initialize(hiddenWeights, Hidden1Count, Hidden2Count);
        Initialize(outputWeights, Hidden2Count, labels.Length);
    }

    private void Initialize(double[,] weights, int fanIn, int fanOut)
    {
        double limit = Math.Sqrt(6.0 / (fanIn + fanOut));
        for (int row = 0; row < weights.GetLength(0); row++)
        {
            for (int column = 0; column < weights.GetLength(1); column++)
            {
                weights[row, column] = (random.NextDouble() * 2 - 1) * limit;
            }
        }
    }

    private static double[] Features(string text)
    {
        double[] features = new double[InputCount];
        string normalized = $" {text.Trim().ToLowerInvariant()} ";

        for (int i = 0; i < normalized.Length - 1; i++)
        {
            features[Hash(normalized.Substring(i, 2)) % InputCount] += 1.0;
        }

        for (int i = 0; i < normalized.Length - 2; i++)
        {
            features[Hash(normalized.Substring(i, 3)) % InputCount] += 0.5;
        }

        double norm = Math.Sqrt(features.Sum(value => value * value));
        if (norm > 0)
        {
            for (int i = 0; i < features.Length; i++)
            {
                features[i] /= norm;
            }
        }

        return features;
    }

    private static int Hash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in text)
            {
                hash = hash * 31 + character;
            }

            return hash & 0x7fffffff;
        }
    }

    private static double[] Activate(double[,] weights, double[] biases, double[] inputs)
    {
        double[] outputs = new double[biases.Length];
        for (int row = 0; row < outputs.Length; row++)
        {
            double value = biases[row];
            for (int column = 0; column < inputs.Length; column++)
            {
                value += weights[row, column] * inputs[column];
            }

            outputs[row] = Math.Tanh(value);
        }

        return outputs;
    }

    private double[] Score(double[] inputs)
    {
        double[] scores = new double[labels.Length];
        for (int row = 0; row < scores.Length; row++)
        {
            double score = outputBiases[row];
            for (int column = 0; column < inputs.Length; column++)
            {
                score += outputWeights[row, column] * inputs[column];
            }

            scores[row] = score;
        }

        return scores;
    }

    private static double[] Softmax(double[] scores)
    {
        double max = scores.Max();
        double[] exponentials = scores.Select(score => Math.Exp(score - max)).ToArray();
        double sum = exponentials.Sum();
        return exponentials.Select(value => value / sum).ToArray();
    }

    private static void Update(double[,] weights, double[] biases, double[] inputs, double[] deltas)
    {
        for (int row = 0; row < weights.GetLength(0); row++)
        {
            biases[row] -= LearningRate * deltas[row];
            for (int column = 0; column < weights.GetLength(1); column++)
            {
                weights[row, column] -= LearningRate * deltas[row] * inputs[column];
            }
        }
    }

    private static double[] Flatten(double[,] values)
    {
        double[] flattened = new double[values.Length];
        int columns = values.GetLength(1);
        for (int row = 0; row < values.GetLength(0); row++)
        {
            for (int column = 0; column < columns; column++)
            {
                flattened[(row * columns) + column] = values[row, column];
            }
        }

        return flattened;
    }

    private static double[,] Expand(double[] values, int rows, int columns)
    {
        double[,] expanded = new double[rows, columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                expanded[row, column] = values[(row * columns) + column];
            }
        }

        return expanded;
    }

    private static bool TryReadValues(JsonElement root, string propertyName, int length, out double[] values)
    {
        values = [];
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return false;
        }

        double[]? parsed = element.Deserialize<double[]>();
        if (parsed is null || parsed.Length != length || parsed.Any(value => !double.IsFinite(value)))
        {
            return false;
        }

        values = parsed;
        return true;
    }
}
