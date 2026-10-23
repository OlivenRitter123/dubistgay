
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

    public long In(string name)
    {
        return Input.GetValueOrDefault(name);
    }

    public long Out(string name)
    {
        return Output.GetValueOrDefault(name);
    }
}

class State
{
    public Dictionary<string, long> Inventory = new();
    public List<string> Steps = new();
    public long Cost = 0;

    public long Get(string name)
    {
        return Inventory.GetValueOrDefault(name);
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

    static string targetName = "";

    const int MaxSteps = 1000000;

    // Nächste nicht-leere Zeile
    static string NextLine()
    {
        while (index < lines.Length &&
               string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        if (index >= lines.Length)
            throw new Exception("Unerwartetes Dateiende!");

        return lines[index++].Trim();
    }

    // Molekülliste einlesen
    static Dictionary<string, long> ParseMolecules(string text)
    {
        Dictionary<string, long> result = new();

        foreach (string part in text.Split(','))
        {
            string value = part.Trim();

            if (value.Length == 0)
                continue;

            string[] p = value.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);

            long amount = long.Parse(p[0]);
            string name = p[1];

            result[name] =
                result.GetValueOrDefault(name) + amount;
        }

        return result;
    }

    // Ziel einlesen: 24C6H12O6 oder 24 C6H12O6
    static (long count, string name) ParseTarget(string text)
    {
        text = text.Trim();

        Match match = Regex.Match(
            text,
            @"^(\d+)\s*([A-Za-z][A-Za-z0-9]*)$"
        );

        if (match.Success)
        {
            return (
                long.Parse(match.Groups[1].Value),
                match.Groups[2].Value
            );
        }

        // Ohne Anzahl bedeutet 1
        return (1, text);
    }

    // Ungefähre Herstellungskosten ermitteln
    static void CalculateEstimates()
    {
        estimates.Clear();

        foreach (var p in prices)
        {
            estimates[p.Key] = p.Value;
        }

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
                        input.Key, out double price))
                    {
                        possible = false;
                        break;
                    }

                    cost += input.Value * price;
                }

                if (!possible)
                    continue;

                foreach (var output in r.Output)
                {
                    double unitCost =
                        cost / output.Value;

                    if (!estimates.ContainsKey(output.Key) ||
                        unitCost < estimates[output.Key] - 0.000001)
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

    // Reaktion nach geschätzten Kosten bewerten
    static double Score(
        State state,
        Reaction r,
        string name,
        long needed)
    {
        double cost = 0;

        foreach (var input in r.Input)
        {
            long missing = Math.Max(
                0,
                input.Value - state.Get(input.Key)
            );

            if (missing == 0)
                continue;

            if (!estimates.TryGetValue(
                input.Key, out double price))
            {
                return double.PositiveInfinity;
            }

            cost += missing * price;
        }

        long gain = r.Out(name) - r.In(name);

        if (gain <= 0)
            return double.PositiveInfinity;

        long remaining = Math.Max(
            1,
            needed - state.Get(name)
        );

        return cost / Math.Min(gain, remaining);
    }

    // Moleküle im Inventar sicherstellen
    static bool Ensure(
        State state,
        string name,
        long needed,
        HashSet<string> active,
        bool canBuy)
    {
        if (state.Get(name) >= needed)
            return true;

        bool buyable =
            canBuy && prices.ContainsKey(name);

        // Zyklische Reaktionen verhindern
        if (active.Contains(name))
            return buyable;

        active.Add(name);

        try
        {
            int attempts = 0;

            while (state.Get(name) < needed)
            {
                attempts++;

                if (attempts > MaxSteps ||
                    state.Steps.Count >= MaxSteps)
                {
                    return false;
                }

                List<Reaction> options = new();

                if (producers.TryGetValue(
                    name, out var available))
                {
                    options = available
                        .Where(r =>
                            r.Out(name) > r.In(name))
                        .Where(r =>
                            name == targetName ||
                            !r.Input.ContainsKey(targetName))
                        .OrderBy(r =>
                            Score(state, r, name, needed))
                        .ToList();
                }

                double bestScore =
                    options.Count > 0
                    ? Score(state, options[0], name, needed)
                    : double.PositiveInfinity;

                // Günstiger direkt kaufen
                if (buyable && prices[name] <= bestScore)
                {
                    return true;
                }

                bool successful = false;

                foreach (Reaction r in options)
                {
                    State backup = state.Copy();
                    long before = state.Get(name);

                    if (Execute(state, r, active) &&
                        state.Get(name) > before)
                    {
                        successful = true;
                        break;
                    }

                    // Fehlgeschlagenen Versuch rückgängig machen
                    state.Restore(backup);
                }

                if (!successful)
                {
                    // Falls Herstellung nicht klappt:
                    // kaufen, sofern erlaubt
                    return buyable;
                }
            }

            return true;
        }
        finally
        {
            active.Remove(name);
        }
    }

    // Eine Reaktion ausführen
    static bool Execute(
        State state,
        Reaction r,
        HashSet<string> active)
    {
        if (state.Steps.Count >= MaxSteps)
            return false;

        // Zwischenprodukte herstellen
        // Mehrere Durchläufe wegen Abhängigkeiten
        for (int pass = 0;
             pass < r.Input.Count + 2;
             pass++)
        {
            foreach (var input in r.Input)
            {
                if (state.Get(input.Key) >= input.Value)
                    continue;

                bool ok = Ensure(
                    state,
                    input.Key,
                    input.Value,
                    active,
                    true
                );

                if (!ok)
                    return false;
            }

            // Alle nicht kaufbaren Stoffe vorhanden?
            bool ready = true;

            foreach (var input in r.Input)
            {
                if (state.Get(input.Key) < input.Value &&
                    !prices.ContainsKey(input.Key))
                {
                    ready = false;
                    break;
                }
            }

            if (ready)
                break;

            if (pass == r.Input.Count + 1)
                return false;
        }

        // Fehlende kaufbare Stoffe kaufen
        foreach (var input in r.Input)
        {
            long available = state.Get(input.Key);

            long missing = Math.Max(
                0,
                input.Value - available
            );

            if (missing > 0)
            {
                if (!prices.TryGetValue(
                    input.Key, out long price))
                {
                    return false;
                }

                state.Cost += missing * price;
            }
        }

        // Reaktionsstoffe verbrauchen
        foreach (var input in r.Input)
        {
            state.Inventory[input.Key] =
                Math.Max(
                    0,
                    state.Get(input.Key) - input.Value
                );
        }

        // Produkte hinzufügen
        foreach (var output in r.Output)
        {
            state.Inventory[output.Key] =
                state.Get(output.Key) + output.Value;
        }

        // Reaktion für Ausgabedatei merken
        state.Steps.Add(r.Text);

        return true;
    }

    // Hauptprogramm
    static void Main(string[] args)
    {
        // ======================================
        // HIER KANNST DU DEN DATEIPFAD ÄNDERN
        // ======================================

        string inputFile =
            args.Length > 0 ? args[0] : "input.txt";

        string outputFile =
            args.Length > 1 ? args[1] : "output.txt";

        if (!File.Exists(inputFile))
        {
            Console.WriteLine(
                "Datei nicht gefunden: " +
                Path.GetFullPath(inputFile)
            );
            return;
        }

        lines = File.ReadAllLines(inputFile);
        index = 0;

        // ======================================
        // 1. KAUFBARE MOLEKÜLE
        // ======================================

        int m = int.Parse(NextLine());

        for (int i = 0; i < m; i++)
        {
            string[] p = NextLine().Split("->");

            long price = long.Parse(p[0].Trim());
            string name = p[1].Trim();

            prices[name] = price;
        }

        // ======================================
        // 2. ALLE VERFÜGBAREN REAKTIONEN
        // ======================================

        int rCount = int.Parse(NextLine());

        for (int i = 0; i < rCount; i++)
        {
            string line = NextLine();
            string[] sides = line.Split("->");

            Reaction r = new Reaction();

            r.Text = line;
            r.Input = ParseMolecules(sides[0]);
            r.Output = ParseMolecules(sides[1]);

            reactions.Add(r);

            foreach (string molecule in r.Output.Keys)
            {
                if (!producers.ContainsKey(molecule))
                {
                    producers[molecule] =
                        new List<Reaction>();
                }

                producers[molecule].Add(r);
            }
        }

        // Geschätzte Preise berechnen
        CalculateEstimates();

        // ======================================
        // 3. ZIELMOLEKÜLE
        // ======================================

        int n = int.Parse(NextLine());

        List<string> results = new();

        for (int i = 0; i < n; i++)
        {
            var target = ParseTarget(NextLine());

            targetName = target.name;

            // Für jeden Fall neues Inventar
            State state = new State();

            HashSet<string> active = new();

            bool success = Ensure(
                state,
                target.name,
                target.count,
                active,
                false
            );

            // Ergebnis kontrollieren
            if (!success ||
                state.Get(target.name) < target.count)
            {
                Console.WriteLine(
                    "FEHLER bei Fall " + (i + 1)
                );

                Console.WriteLine(
                    "Keine Reaktionsfolge gefunden für " +
                    target.count + " " + target.name
                );

                return;
            }

            // Nur Reaktionen ausgeben
            results.Add(
                string.Join("\n", state.Steps)
            );

            Console.WriteLine(
                "Fall " + (i + 1) + " fertig"
            );

            Console.WriteLine(
                "Ziel: " + target.count +
                " " + target.name
            );

            Console.WriteLine(
                "Kosten: " + state.Cost
            );

            Console.WriteLine(
                "Reaktionen: " + state.Steps.Count
            );

            Console.WriteLine();
        }

        // ======================================
        // 4. OUTPUT DATEI ERSTELLEN
        // ======================================

        File.WriteAllText(
            outputFile,
            string.Join("\n\n", results) + "\n"
        );

        Console.WriteLine(
            "FERTIG! Datei erstellt: " + outputFile
        );
    }
}
