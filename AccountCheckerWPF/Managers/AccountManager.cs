using System.IO;

namespace AccountCheckerWPF.Managers;

public class AccountManager
{
    public List<string> Accounts { get; } = new();

    public void LoadFromFile(string filename)
    {
        try
        {
            using var reader = new StreamReader(filename);
            while (reader.ReadLine() is { } line)
            {
                Accounts.Add(line);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load combos from file", ex);
        }
    }
}