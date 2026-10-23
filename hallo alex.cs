
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Reaction
{
    public string Text = "";
    public Dictionary<string, long> Input = new();
    public Dictionary<string, long> Output = new();
}

class State
{
    public Dictionary<string, long> Inventory = new();
    public List<string> Steps = new();
    public long Cost = 0;

    public long Get(string name)
    {
        return Inventory.GetValueOrDefault(name, 0);
    }

    public State Copy()
    {
        return new State
        {
            Inventory = new Dictionary<string, long>(Inventory),
            Steps = new List<string>(Steps),
            Cost = Cost
        };
    }

    public void Restore(State other)
    {
        Inventory = other.Inventory;
        Steps = other.Steps;
        Cost = other.Cost;
    }
}

class Program
{
    static Dictionary<string, long> prices = new();
    static List<Reaction> reactions = new();
    static Dictionary<string, List<Reaction>> producers = new();
    static Dictionary<string, double> estimates = new();

    static string[] lines = Array.Empty<string>();
    static int index = 0;

    static string NextLine()
    {
        while (index < lines.Length &&
               string.IsNullOrWhiteSpace(lines[index]))
            index++;

        if (index >= lines.Length)
            throw new Exception("Unerwartetes Dateiende");

        return lines[index++].Trim();
    }

    static Dictionary<string, long> Parse(string text)
    {
        Dictionary<string, long> result = new();

        foreach (string part in text.Split(','))
        {
            string[] p = part.Trim().Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);

            if (p.Length < 2)
                continue;

            long count = long.Parse(p[0]);
            string name = p[1];

            result[name] = result.GetValueOrDefault(name) + count;
        }

        return result;
    }

    static void CalculateEstimates()
    {
        estimates.Clear();

        foreach (var p in prices)
            estimates[p.Key] = p.Value;

        // Geschätzte Herstellungskosten berechnen
        for (int i = 0; i < 100; i++)
        {
            bool changed = false;

            foreach (Reaction r in reactions)
            {
                double cost = 0;
                bool possible = true;

                foreach (var input in r.Input)
                {
                    if (!estimates.TryGetValue(
                        input.Key, out double value))
                    {
                        possible = false;
                        break;
                    }

                    cost += input.Value * value;
                }

                if (!possible)
                    continue;

                foreach (var output in r.Output)
                {
                    double unitCost = cost / output.Value;

                    if (!estimates.ContainsKey(output.Key) ||
                        unitCost < estimates[output.Key] - 0.0000001)
                    {
                        estimates[output.Key] = unitCost;
                        changed = true;
                    }
                }
            }

            if (!changed)
                break;
        }
    }

    static double Score(
        State state, Reaction r, string target, long needed)
    {
        double cost = 0;

        foreach (var input in r.Input)
        {
            long missing = Math.Max(
                0, input.Value - state.Get(input.Key));

            if (missing == 0)
                continue;

            if (!estimates.TryGetValue(
                input.Key, out double unitCost))
                return double.PositiveInfinity;

            cost += missing * unitCost;
        }

        long outputCount = r.Output[target];
        long missingTarget = Math.Max(
            1, needed - state.Get(target));

        return cost / Math.Min(outputCount, missingTarget);
    }

    static bool Ensure(
        State state,
        string name,
        long needed,
        HashSet<string> active,
        bool allowBuying)
    {
        if (state.Get(name) >= needed)
            return true;

        bool canBuy = allowBuying && prices.ContainsKey(name);

        // Zyklen vermeiden
        if (active.Contains(name))
            return canBuy;

        active.Add(name);

        try
        {
            while (state.Get(name) < needed)
            {
                if (state.Steps.Count >= 1000000)
                    return false;

                List<Reaction> options = new();

                if (producers.TryGetValue(name, out var available))
                {
                    options = available
                        .Where(r =>
                            r.Output[name] >
                            r.Input.GetValueOrDefault(name))
                        .OrderBy(r => Score(state, r, name, needed))
                        .ToList();
                }

                double bestScore = options.Count > 0
                    ? Score(state, options[0], name, needed)
                    : double.PositiveInfinity;

                // Kaufen ist billiger als Herstellen:
                // Der eigentliche Kauf erfolgt bei der Reaktion.
                if (canBuy && prices[name] <= bestScore)
                    return true;

                bool produced = false;

                foreach (Reaction r in options)
                {
                    State backup = state.Copy();
                    long before = state.Get(name);

                    if (Execute(state, r, active) &&
                        state.Get(name) > before)
                    {
                        produced = true;
                        break;
                    }

                    state.Restore(backup);
                }

                if (!produced)
                    return canBuy;
            }

            return true;
        }
        finally
        {
            active.Remove(name);
        }
    }

    static bool Execute(
        State state,
        Reaction r,
        HashSet<string> active)
    {
        // Benötigte Zwischenprodukte herstellen
        foreach (var input in r.Input)
        {
            if (!Ensure(
                state, input.Key, input.Value, active, true))
                return false;
        }

        // Prüfen, ob alle nicht kaufbaren Stoffe da sind
        for (int attempt = 0;
             attempt <= r.Input.Count * 3;
             attempt++)
        {
            bool missing = false;

            foreach (var input in r.Input)
            {
                if (prices.ContainsKey(input.Key))
                    continue;

                if (state.Get(input.Key) < input.Value)
                {
                    missing = true;

                    if (!Ensure(
                        state, input.Key, input.Value,
                        active, false))
                        return false;
                }
            }

            if (!missing)
                break;
        }

        // Fehlende kaufbare Moleküle automatisch kaufen
        foreach (var input in r.Input)
        {
            long available = state.Get(input.Key);
            long missing = Math.Max(0, input.Value - available);

            if (missing > 0)
            {
                if (!prices.TryGetValue(
                    input.Key, out long price))
                    return false;

                state.Cost += missing * price;
            }
        }

        // Eingabemoleküle verbrauchen
        foreach (var input in r.Input)
        {
            state.Inventory[input.Key] =
                Math.Max(0, state.Get(input.Key) - input.Value);
        }

        // Produkte ins Inventar legen
        foreach (var output in r.Output)
        {
            state.Inventory[output.Key] =
                state.Get(output.Key) + output.Value;
        }

        // Ausgeführte Reaktion speichern
        state.Steps.Add(r.Text);

        return true;
    }

    static (long count, string name) ParseTarget(string text)
    {
        text = text.Trim();

        // Akzeptiert "24C6H12O6", "24 C6H12O6"
        // und "C2H5OH" (automatisch Menge 1)
        Match match = Regex.Match(
            text, @"^(\d+)\s*([A-Za-z][A-Za-z0-9]*)$");

        if (match.Success)
        {
            return (
                long.Parse(match.Groups[1].Value),
                match.Groups[2].Value
            );
        }

        return (1, text);
    }

    static void Main(string[] args)
    {
        lines = File.ReadAllLines("input.txt");
        index = 0;

        // Kaufbare Moleküle
        int m = int.Parse(NextLine());

        for (int i = 0; i < m; i++)
        {
            string[] p = NextLine().Split("->");

            long price = long.Parse(p[0].Trim());
            string name = p[1].Trim();

            prices[name] = price;
        }

        // Mögliche Reaktionen
        int rCount = int.Parse(NextLine());

        for (int i = 0; i < rCount; i++)
        {
            string line = NextLine();
            string[] sides = line.Split("->");

            Reaction r = new Reaction
            {
                Text = line,
                Input = Parse(sides[0]),
                Output = Parse(sides[1])
            };

            reactions.Add(r);

            foreach (string name in r.Output.Keys)
            {
                if (!producers.ContainsKey(name))
                    producers[name] = new List<Reaction>();

                producers[name].Add(r);
            }
        }

        CalculateEstimates();

        // Zielmoleküle
        int n = int.Parse(NextLine());

        List<string> paragraphs = new();

        for (int i = 0; i < n; i++)
        {
            var target = ParseTarget(NextLine());

            State state = new State();

            bool success = Ensure(
                state,
                target.name,
                target.count,
                new HashSet<string>(),
                false
            );

            if (!success || state.Get(target.name) < target.count)
            {
                throw new Exception(
                    "Keine Reaktionsfolge gefunden für " +
                    target.name
                );
            }

            paragraphs.Add(string.Join("\n", state.Steps));

            Console.WriteLine(
                $"Fall {i + 1}: {target.count} {target.name}, " +
                $"Kosten: {state.Cost}, " +
                $"Reaktionen: {state.Steps.Count}"
            );
        }

        // Leere Zeile zwischen den Testfällen
        File.WriteAllText(
            "output.txt",
            string.Join("\n\n", paragraphs) + "\n"
        );

        Console.WriteLine("Fertig! output.txt erstellt.");
    }
}
