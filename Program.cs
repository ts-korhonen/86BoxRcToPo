using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace _86BoxRcToPo
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2 || !Directory.Exists(args[0]) || !Directory.Exists(args[1]))
            {
                Console.WriteLine("Converter for 86Box language files from .rc to .po");
                Console.WriteLine();
                Console.WriteLine($"Usage: {Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)} input-path output-path");
                Console.WriteLine();
                Console.WriteLine("Input path should point to 86Box local repository folder (src\\win\\languages).");
                Console.WriteLine("Output path should point to 86Box local repository folder (src\\qt\\languages).");

                return;
            }

            var input = Directory.EnumerateFiles(args[0], "*.rc").Where(x => Regex.IsMatch(x, @"\w{2}-\w{2}.rc", RegexOptions.IgnoreCase));

            // Parse rc files
            var parsed = input.ToDictionary(
                fn => Path.GetFileNameWithoutExtension(fn),
                fn => ParseRcLines(File.ReadAllLines(fn)).ToList(),
                StringComparer.InvariantCultureIgnoreCase);

            if (!parsed.ContainsKey("en-US"))
            {
                Console.WriteLine("Language en-US not found! Check the input-path.");
                return;
            }

            // Loop each language
            foreach (var lang in parsed)
            {
                // Pair with en-US translation
                var pairs = parsed["en-US"].Zip(lang.Value, (original, translation) =>
                {
                    if (original.id != translation.id)
                        Console.WriteLine($"Warning: ID mismatch: {original.id} vs {translation.id}");

                    // Keep font variables as id's

                    if (original.id == "FONT_NAME")
                        return (id: "FONT_NAME", str: translation.str);

                    if (original.id == "FONT_SIZE")
                        return (id: "FONT_SIZE", str: translation.str);

                    return (id: original.str, str: translation.str);
                });

                // Output file path
                string path = Path.ChangeExtension(Path.Combine(args[1], lang.Key), "po");

                Console.WriteLine($"Writing {path}");

                using var output = File.CreateText(path);

                // Use LF line ending 
                output.NewLine = "\n";

                // Cache to keep track of translated lines to check duplicates
                Dictionary<string,string> cache = new();

                // Write id/str pairs to file in .po format
                foreach (var pair in pairs)
                {
                    if (!cache.TryAdd(pair.id, pair.str))
                    {
                        if (cache[pair.id] != pair.str)
                            Console.WriteLine($"Warning: Omited translation due to duplicate source string: {pair.str}");
                        continue;
                    }

                    output.WriteLine($"msgid \"{pair.id}\"");
                    output.WriteLine($"msgstr \"{pair.str}\"");
                    output.WriteLine();
                }
            }
        }

        /// <summary>
        ///     Parses lines of .rc
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        static IEnumerable<(string id, string str)> ParseRcLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var menuLine = Regex.Match(line, @"^\s*(POPUP|MENUITEM)\s*""(.+)""");

                if (menuLine.Success)
                    yield return (null, Convert(menuLine.Groups[2].Value));

                var defineLine = Regex.Match(line, @"^\s*#define\s+(\w+)\s+(""?)(.+)\2");

                if (defineLine.Success)
                {
                    // Leave this define out
                    if (defineLine.Groups[1].Value == "IDS_LANG_ENUS")
                        continue;

                    yield return (defineLine.Groups[1].Value, Convert(defineLine.Groups[3].Value));
                }

                var ids_line = Regex.Match(line, @"^\s*((IDS_)?\d+).+?""(.+)""");

                if (ids_line.Success)
                {
                    var filedlg = ExtractFileDialogStrings(ids_line.Groups[3].Value);

                    if (filedlg is not null)
                    {
                        int id = 1;

                        foreach (string str in filedlg)
                            yield return ($"{ids_line.Groups[1].Value}_{id}", str);
                    }
                    else
                        yield return (ids_line.Groups[1].Value, Convert(ids_line.Groups[3].Value));
                }
            }
        }

        /// <summary>
        ///     Tries to extract extension descriptions from file dialog filter
        /// </summary>
        /// <param name="str"></param>
        /// <returns>descriptions or null if not a filter</returns>
        static IEnumerable<string> ExtractFileDialogStrings(string str)
        {
            string[] test = Regex.Split(str, @"\(.+?\)\\0.+?\\0");

            if (test.Length > 1)
                return test.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x));
            else
                return null;
        }

        /// <summary>
        ///     Converts quotes to qt format, replaces constants
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static string Convert(string str) => str
                .Replace("\" LIB_NAME_PCAP \"", "libpcap")
                .Replace("\" LIB_NAME_FREETYPE \"", "libfreetype")
                .Replace("\" LIB_NAME_FLUIDSYNTH \"", "libfluidsynth")
                .Replace("\" LIB_NAME_GS \"", "libgs")
                .Replace("\"\"", "\\\"");
    }
}
