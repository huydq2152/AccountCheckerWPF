using System.IO;

namespace AccountCheckerWPF.Managers;

public class ComboManager
{
    public List<string> ComboList { get; private set; } = new List<string>();

    public int LoadFromFile(string filename)
    {
        try
        {
            using (var reader = new StreamReader(filename))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ComboList.Add(line);
                }
            }

            return ComboList.Count;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load combos from file", ex);
        }
    }
}