
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

string[] lines = File.ReadAllLines("input.txt");
int index = 0;

// Überspringt leere Zeilen
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
List<string> results = new();

for (int i = 0; i < n; i++)
{
    Dictionary<string, long> inventory = new();
    List<string> order = new();

    // Moleküle hinzufügen oder entfernen
    void Change(string name, long amount)
    {
        if (!inventory.ContainsKey(name))
        {
            inventory[name] = 0;
            order.Add(name);
        }

        inventory[name] += amount;
    }

    // Molekülliste verarbeiten
    void ParseMolecules(string text, int sign)
    {
        foreach (string molecule in text.Split(','))
        {
            string[] parts = molecule.Trim().Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);

            long count = long.Parse(parts[0]);
            string name = parts[1];

            Change(name, count * sign);
        }
    }

    // Anfangsinventar
    string start = NextLine();
    ParseMolecules(start, 1);

    // Reaktion
    string reaction = NextLine();
    string[] sides = reaction.Split("->");

    // Ausgangsstoffe entfernen
    ParseMolecules(sides[0], -1);

    // Produkte hinzufügen
    ParseMolecules(sides[1], 1);

    // Ergebnis in ursprünglicher Reihenfolge
    string result = string.Join(", ",
        order
            .Where(name => inventory[name] > 0)
            .Select(name => $"{inventory[name]} {name}")
    );

    results.Add(result);
}

// Ausgabedatei erstellen
File.WriteAllLines("output.txt", results);
