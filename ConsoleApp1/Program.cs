const string file1Path = @"C:\MyProjects\Backend\MyProjects\ConsoleProjectTest\ConsoleApp1\NewFile1.txt"; 

const string outputPath = @"C:\MyProjects\Backend\MyProjects\ConsoleProjectTest\ConsoleApp1\output.txt";

var text = File.ReadAllText(file1Path);

var codes = text
    .Split(new[] { ',', '\r', '\n', ' ', '\t' },
        StringSplitOptions.RemoveEmptyEntries)
    .Select(s => s.Trim())
    .Where(s => !string.IsNullOrEmpty(s));

var outputLines = codes
    .Select(code => $"\"{code}\",");

File.WriteAllLines(outputPath, outputLines);


foreach (var line in outputLines)
{
    Console.WriteLine(line);
}


Console.WriteLine($"\n\nRESULT count:  {outputLines.Count()}.");
Console.WriteLine("END CODE !!!============================!!!:");