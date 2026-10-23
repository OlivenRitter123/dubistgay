
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        string[] lines = File.ReadAllLines("input.txt");
        int index = 0;

        // Nächste nicht-leere Zeile lesen
        string NextLine()
        {
            while (index < lines.Length &&
                   string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            return lines[index++].Trim();
        }

        // Molekülliste lesen
        Dictionary<string, long> ParseMolecules(string text)
        {
            Dictionary<string, long> molecules = new();

            foreach (string part in text.Split(','))
            {
                string[] pieces = part.Trim().Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                );

                long amount = long.Parse(pieces[0]);
                string name = pieces[1];

                if (!molecules.ContainsKey(name))
                    molecules[name] = 0;

                molecules[name] += amount;
            }

            return molecules;
        }

        // Preise einlesen
        int m = int.Parse(NextLine());

        Dictionary<string, long> prices = new();

        for (int i = 0; i < m; i++)
        {
            string[] parts = NextLine().Split("->");

            long price = long.Parse(parts[0].Trim());
            string name = parts[1].Trim();

            prices[name] = price;
        }

        // Anzahl der Testfälle
        int n = int.Parse(NextLine());

        List<string> results = new();

        for (int i = 0; i < n; i++)
        {
            // Neues, leeres Inventar
            Dictionary<string, long> inventory = new();

            long totalCost = 0;

            int r = int.Parse(NextLine());

            // Alle Reaktionen durchführen
            for (int j = 0; j < r; j++)
            {
                string reaction = NextLine();
                string[] sides = reaction.Split("->");

                var inputs = ParseMolecules(sides[0]);
                var outputs = ParseMolecules(sides[1]);

                // Benötigte Moleküle verbrauchen
                foreach (var molecule in inputs)
                {
                    string name = molecule.Key;
                    long needed = molecule.Value;

                    long available = inventory.ContainsKey(name)
                        ? inventory[name]
                        : 0;

                    if (available < needed)
                    {
                        // Fehlende Moleküle kaufen
                        long missing = needed - available;

                        totalCost += missing * prices[name];

                        inventory[name] = 0;
                    }
                    else
                    {
                        inventory[name] = available - needed;
                    }
                }

                // Produzierte Moleküle hinzufügen
                foreach (var molecule in outputs)
                {
                    string name = molecule.Key;
                    long amount = molecule.Value;

                    if (!inventory.ContainsKey(name))
                        inventory[name] = 0;

                    inventory[name] += amount;
                }
            }

            // Inventar ausgeben
            string resultInventory = string.Join(", ",
                inventory
                    .Where(x => x.Value > 0)
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => $"{x.Value} {x.Key}")
            );

            results.Add($"{totalCost} -> {resultInventory}");
        }

        // Ausgabedatei erstellen
        File.WriteAllLines("output.txt", results);

        Console.WriteLine("Fertig! output.txt erstellt.");
    }
}
