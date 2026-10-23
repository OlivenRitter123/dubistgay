
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        string[] lines = File.ReadAllLines("in_level-1_0-example.txt");
        int index = 0;

        string NextLine()
        {
            while (index < lines.Length &&
                   string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            return lines[index++].Trim();
        }

        int n = int.Parse(NextLine());
        List<string> results = new List<string>();

        for (int i = 0; i < n; i++)
        {
            Dictionary<string, long> inventory =
                new Dictionary<string, long>();

            List<string> order = new List<string>();

            void Change(string name, long amount)
            {
                if (!inventory.ContainsKey(name))
                {
                    inventory[name] = 0;
                    order.Add(name);
                }

                inventory[name] += amount;
            }

            void ParseMolecules(string text, int sign)
            {
                string[] molecules = text.Split(',');

                foreach (string molecule in molecules)
                {
                    string[] parts = molecule.Trim().Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries
                    );

                    long count = long.Parse(parts[0]);
                    string name = parts[1];

                    Change(name, count * sign);
                }
            }

            // Inventar einlesen
            ParseMolecules(NextLine(), 1);

            // Reaktion einlesen
            string reaction = NextLine();
            string[] sides = reaction.Split("->");

            // Moleküle entfernen
            ParseMolecules(sides[0], -1);

            // Moleküle hinzufügen
            ParseMolecules(sides[1], 1);

            // Ergebnis erstellen
            string result = string.Join(", ",
                order
                    .Where(name => inventory[name] > 0)
                    .Select(name => $"{inventory[name]} {name}")
            );

            results.Add(result);
        }

        // Datei schreiben
        File.WriteAllLines("output.txt", results);

        Console.WriteLine("Fertig! output.txt erstellt.");
    }
}
