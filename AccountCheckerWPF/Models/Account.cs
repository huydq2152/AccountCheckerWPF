namespace AccountCheckerWPF.Models;

public class Account
{
    public string Combo { get; set; }

    private List<string> Capture { get; set; } = new List<string>();

    public void AddCaptureStr(string name, string data)
    {
        Capture.Add($"{name}: {data}");
    }

    public void AddCaptureInt(string name, int data)
    {
        Capture.Add($"{name}: {data}");
    }

    public override string ToString()
    {
        return $"{Combo} [ {string.Join(" | ", Capture)} ]";
    }
}