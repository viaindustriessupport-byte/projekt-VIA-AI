using System.Text.Json;
using System.IO;

namespace KiLernprojekt;

public sealed class IntentNetwork
{
    private readonly string[] labels;
    private readonly double[,] weights;
    private readonly Random random = new(42);
    private const int FeatureCount = 256;

    public IntentNetwork(string[] labels)
    {
        this.labels = labels;
        weights = new double[labels.Length, FeatureCount];
        for (int label = 0; label < labels.Length; label++)
        {
            for (int feature = 0; feature < FeatureCount; feature++)
            {
                weights[label, feature] = (random.NextDouble() - 0.5) * 0.01;
            }
        }
    }

    public void Train(string text, string label, int epochs = 30)
    {
        int labelIndex = Array.IndexOf(labels, label);
        if (labelIndex < 0)
        {
            return;
        }

        double[] features = Features(text);
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            double[] scores = new double[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                for (int feature = 0; feature < FeatureCount; feature++)
                {
                    scores[i] += weights[i, feature] * features[feature];
                }
            }

            double[] probabilities = Softmax(scores);
            for (int i = 0; i < labels.Length; i++)
            {
                double error = (i == labelIndex ? 1.0 : 0.0) - probabilities[i];
                for (int feature = 0; feature < FeatureCount; feature++)
                {
                    weights[i, feature] += 0.1 * error * features[feature];
                }
            }
        }
    }

    public string Predict(string text)
    {
        double[] features = Features(text);
        int bestLabel = 0;
        double bestScore = double.MinValue;

        for (int i = 0; i < labels.Length; i++)
        {
            double score = 0;
            for (int feature = 0; feature < FeatureCount; feature++)
            {
                score += weights[i, feature] * features[feature];
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestLabel = i;
            }
        }

        return labels[bestLabel];
    }

    public void Save(string fileName)
    {
        double[] flatWeights = new double[weights.Length];
        int rows = weights.GetLength(0);
        int columns = weights.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                flatWeights[(row * columns) + column] = weights[row, column];
            }
        }

        var model = new
        {
            Labels = labels,
            Rows = rows,
            Columns = columns,
            Weights = flatWeights
        };
        File.WriteAllText(fileName, JsonSerializer.Serialize(model));
    }

    public void Load(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fileName));
        JsonElement labelsElement = document.RootElement.GetProperty("Labels");
        JsonElement weightsElement = document.RootElement.GetProperty("Weights");
        int rows = document.RootElement.GetProperty("Rows").GetInt32();
        int columns = document.RootElement.GetProperty("Columns").GetInt32();

        string[] savedLabels = labelsElement.Deserialize<string[]>() ?? [];
        if (savedLabels.Length != labels.Length)
        {
            return;
        }

        double[]? savedWeights = weightsElement.Deserialize<double[]>();
        if (savedWeights is null ||
            rows != weights.GetLength(0) ||
            columns != weights.GetLength(1) ||
            savedWeights.Length != weights.Length)
        {
            return;
        }

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                weights[row, column] = savedWeights[(row * columns) + column];
            }
        }
    }

    private static double[] Features(string text)
    {
        double[] features = new double[FeatureCount];
        string normalized = $" {text.Trim().ToLowerInvariant()} ";

        for (int i = 0; i < normalized.Length - 1; i++)
        {
            int index = Hash(normalized.Substring(i, 2)) % FeatureCount;
            features[index] += 1.0;
        }

        for (int i = 0; i < normalized.Length - 2; i++)
        {
            int index = Hash(normalized.Substring(i, 3)) % FeatureCount;
            features[index] += 0.5;
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

    private static double[] Softmax(double[] scores)
    {
        double max = scores.Max();
        double[] exponentials = scores.Select(score => Math.Exp(score - max)).ToArray();
        double sum = exponentials.Sum();
        return exponentials.Select(value => value / sum).ToArray();
    }
}
