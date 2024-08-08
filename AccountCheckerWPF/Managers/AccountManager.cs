using System.IO;

namespace AccountCheckerWPF.Managers;

public class AccountManager
{
    public List<string> Accounts { get; } = new();

    public int LoadFromFile(string filename)
    {
        try
        {
            using (var reader = new StreamReader(filename))
            {
                while (reader.ReadLine() is { } line)
                {
                    Accounts.Add(line);
                }
            }

            return Accounts.Count;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load combos from file", ex);
        }
    }
}