using System;
using CommandLine; // https://github.com/commandlineparser/commandline
using nietras.SeparatedValues; // https://github.com/nietras/Sep

namespace CSVIDCompareTool
{
    public class CommandLineOptions
    {

        [Option("file-a", Required = true, HelpText = "Specify file A")]
        public string FileNameA { get;set; } = string.Empty;

        [Option("file-b", Required = true, HelpText = "Specify file B")]
        public string FileNameB { get;set; } = string.Empty;

        [Option("file-a-col-name", Required = false, HelpText = "Specify file A column to compare BY NAME")]
        public string ColumnAName { get; set; } = string.Empty;

        [Option("file-b-col-name", Required = false, HelpText = "Specify file B column to compare BY NAME")]
        public string ColumnBName { get;set; } = string.Empty;

        [Option("file-a-col-num", Required = false, HelpText = "Specify file A column to compare BY COLUMN NUMBER. The first column is 1 (not zero).")]
        public int ColumnANumber { get; set; } = -1;

        [Option("file-b-col-num", Required = false, HelpText = "Specify file B column to compare BY COLUMN NUMBER. The first column is 1 (not zero).")]
        public int ColumnBNumber { get;set; } = -1;

        [Option('d', "delimeter", Required = false, HelpText = "The character used to deliminate columns (Default is a comma)")]
        public char Delimeter { get; set; } = ',';

        [Option('v', "verbose", Required = false, HelpText = "Show extra information on the console as the program runs.")]
        public bool Verbose { get; set; }

        [Option('s', "show-rows", Required = false, HelpText = "Show the entire rows that are different, not just the values from the selected column")]
        public bool ShowFullRows { get; set; }
    }

    internal class Program
    {
        private static string StripCSVEncapsulation(string inputString)
        {
            return inputString.Trim('\"').Trim('\'');
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                   .WithParsed<CommandLineOptions>(Options =>
                   {
                        if (Options.Verbose) { Console.WriteLine("Checking files..."); }
                        // Check if the files actually exist
                        if (File.Exists(Options.FileNameA) == false)
                        {
                            Console.WriteLine($"Specified file (A) does not exist");
                            return;
                        }

                        if (File.Exists(Options.FileNameB) == false)
                        {
                            Console.WriteLine($"Specified file (B) does not exist");
                            return;
                        }

                        using var fileA_reader = Sep.Reader().FromFile(Options.FileNameA);
                        using var fileB_reader = Sep.Reader().FromFile(Options.FileNameB);

                        if (Options.Verbose)
                        {
                            Console.WriteLine($"Found columns in {Options.FileNameA}:");
                            foreach(string colName in fileA_reader.Header.ColNames)
                            {
                                Console.WriteLine($" {StripCSVEncapsulation(colName)}");
                            }
                            Console.WriteLine();

                            Console.WriteLine($"Found columns in {Options.FileNameA}:");
                            foreach(string colName in fileB_reader.Header.ColNames)
                            {
                                Console.WriteLine($" {StripCSVEncapsulation(colName)}");
                            }
                            Console.WriteLine();
                        }

                        // If we got column names, we'll need to figure out which column it is
                        // If we got column numbers, we can skip this section

                        int FileA_ColNum = Options.ColumnANumber;
                        int FileB_ColNum = Options.ColumnBNumber;

                        // Find column number for file A
                        if (!string.IsNullOrEmpty(Options.ColumnAName) && FileA_ColNum <= 0)
                        {
                            if (Options.Verbose)
                            {
                                Console.WriteLine($"Trying to find column with name '{Options.ColumnAName}' in file {Options.FileNameA}... ");
                            }
                            foreach(string colName in fileA_reader.Header.ColNames)
                            {
                                if (StripCSVEncapsulation(Options.ColumnAName).ToLower() == StripCSVEncapsulation(colName).ToLower())
                                {
                                    FileA_ColNum = fileA_reader.Header.IndexOf(colName);
                                    if (Options.Verbose)
                                    {
                                        Console.WriteLine($" Found column {FileA_ColNum+1}.");
                                        Console.WriteLine();
                                    }
                                }
                            }
                        }

                        // Find column number for file B
                        if (!string.IsNullOrEmpty(Options.ColumnBName) && FileB_ColNum <= 0)
                        {
                            if (Options.Verbose)
                            {
                                Console.WriteLine($"Trying to find column with name '{Options.ColumnBName}' in file {Options.FileNameB}... ");
                            }
                            foreach(string colName in fileB_reader.Header.ColNames)
                            {
                                if (StripCSVEncapsulation(Options.ColumnAName).ToLower() == StripCSVEncapsulation(colName).ToLower())
                                {
                                    FileB_ColNum = fileB_reader.Header.IndexOf(colName);
                                    if (Options.Verbose)
                                    {
                                        Console.WriteLine($" Found column {FileB_ColNum+1}.");
                                        Console.WriteLine();
                                    }
                                }
                            }
                        }

                        if (FileA_ColNum == -1)
                        {
                            Console.WriteLine($"Unable to determine which column to use for file '{Options.FileNameA}'.");
                            return;
                        }
                        if (FileB_ColNum == -1)
                        {
                            Console.WriteLine($"Unable to determine which column to use for file '{Options.FileNameB}'.");
                            return;
                        }

                        if (Options.Verbose) {
                            Console.WriteLine($"Using column {FileA_ColNum+1} for file {Options.FileNameA}");
                            Console.WriteLine($"Using column {FileB_ColNum+1} for file {Options.FileNameB}");
                            Console.WriteLine();

                        }

                        if (Options.Verbose) { Console.WriteLine("Column for file A: " + FileA_ColNum); }
                        if (Options.Verbose) { Console.WriteLine("Column for file B: " + FileB_ColNum); }

                        List<string> FileA_UniqueValues = new List<string>();
                        List<string> FileB_UniqueValues = new List<string>();

                        Dictionary<string, List<string>> FileA_FullRows = new Dictionary<string, List<string>>();
                        Dictionary<string, List<string>> FileB_FullRows = new Dictionary<string, List<string>>();

                        int FileA_Uniques = 0;
                        int FileB_Uniques = 0;
                        int FileA_TotalCount = 0;
                        int FileB_TotalCount = 0;

                        // Attempt to load just the one column from file A
                        if (Options.Verbose) { Console.WriteLine("Parsing file A..."); }
                        try {
                            foreach(var row in fileA_reader)
                            {
                                FileA_TotalCount++;
                                string capturedValue = StripCSVEncapsulation(row[FileA_ColNum].ToString());
                                if (!FileA_UniqueValues.Contains(capturedValue))
                                {
                                    FileA_UniqueValues.Add(capturedValue);
                                    FileA_Uniques++;
                                }

                                if (Options.ShowFullRows)
                                {
                                    if (!FileA_FullRows.ContainsKey(capturedValue))
                                    {
                                        FileA_FullRows.Add(capturedValue, new List<string>());
                                    }

                                    FileA_FullRows[capturedValue].Add(row.ToString());
                                }
                            }
                        } catch(Exception ex) {
                            Console.WriteLine(ex);
                            return;
                        }

                        // Attempt to load just the one column from file B
                        if (Options.Verbose) { Console.WriteLine("Parsing file A..."); }
                        try {
                            foreach(var row in fileB_reader)
                            {
                                FileA_TotalCount++;
                                string capturedValue = StripCSVEncapsulation(row[FileB_ColNum].ToString());
                                if (!FileB_UniqueValues.Contains(capturedValue))
                                {
                                    FileB_UniqueValues.Add(capturedValue);
                                    FileB_Uniques++;
                                }

                                if (Options.ShowFullRows)
                                {
                                    if (!FileB_FullRows.ContainsKey(capturedValue))
                                    {
                                        FileB_FullRows.Add(capturedValue, new List<string>());
                                    }
                                    
                                    FileB_FullRows[capturedValue].Add(row.ToString());
                                }
                            }
                        } catch(Exception ex) {
                            Console.WriteLine(ex);
                            return;
                        }




                        // Compare the two columns for differences
                        if (Options.Verbose) { Console.WriteLine("Processing..."); }

                        List<string> FileA_OrphanedValues = new List<string>();
                        List<string> FileB_OrphanedValues = new List<string>();

                        // Find the values from File A that aren't in File B
                        if (Options.Verbose) { Console.WriteLine($" {Options.FileNameA}"); }
                        foreach(string val in FileA_UniqueValues)
                        {
                            if (!FileB_UniqueValues.Contains(val))
                            {
                                FileA_OrphanedValues.Add(val);
                            }
                        }

                        // Find the values from File B that aren't in File A
                        if (Options.Verbose) { Console.WriteLine($" {Options.FileNameB}"); }
                        foreach(string val in FileB_UniqueValues)
                        {
                            if (!FileA_UniqueValues.Contains(val))
                            {
                                FileB_OrphanedValues.Add(val);
                            }
                        }

                        // Output statistics
                        Console.WriteLine("======================================================");
                        Console.WriteLine("=                   STATS                            =");
                        Console.WriteLine("======================================================");
                        Console.WriteLine($" FILE A: {Options.FileNameA}");
                        Console.WriteLine($"   Lines: {FileA_TotalCount}");
                        Console.WriteLine($"   Unique Values: {FileA_Uniques}");
                        Console.WriteLine();
                        Console.WriteLine($" FILE B: {Options.FileNameB}");
                        Console.WriteLine($"   Lines: {FileB_TotalCount}");
                        Console.WriteLine($"   Unique Values: {FileB_Uniques}");
                        Console.WriteLine();

                        Console.WriteLine("======================================================");
                        Console.WriteLine("=                  Unique Values                     =");
                        Console.WriteLine("======================================================");

                        Console.WriteLine($" Values in {Options.FileNameA} that aren't in {Options.FileNameB} ({FileA_OrphanedValues.Count})");

                        if (Options.ShowFullRows) 
                        {
                            foreach(string index in FileA_FullRows.Keys)                            
                            {
                                Console.WriteLine($"  {index}");
                                foreach(string row in FileA_FullRows[index])
                                {
                                    Console.WriteLine($"   {row}");
                                }
                            }
                        } else {
                            foreach(string val in FileA_OrphanedValues)
                            {
                                Console.WriteLine($"  {val}");
                            }
                        }

                        Console.WriteLine();
                        Console.WriteLine($" Values in {Options.FileNameB} that aren't in {Options.FileNameA} ({FileB_OrphanedValues.Count})");
                        
                        if (Options.ShowFullRows) 
                        {
                            foreach(string index in FileB_FullRows.Keys)                            
                            {
                                Console.WriteLine($"  {index}");
                                foreach(string row in FileB_FullRows[index])
                                {
                                    Console.WriteLine($"   {row}");
                                }
                            }
                        } else {
                            foreach(string val in FileB_OrphanedValues)
                            {
                                Console.WriteLine($"  {val}");
                            }
                        }

                        Console.WriteLine();
                   });
        }
    }
}