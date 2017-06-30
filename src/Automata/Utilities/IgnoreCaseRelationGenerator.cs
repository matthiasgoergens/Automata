﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;

namespace Microsoft.Automata.Utilities
{
    internal static class IgnoreCaseRelationGenerator
    {
        public static void Generate(string namespacename, string classname, string path)
        {
            if (classname == null)
                throw new ArgumentNullException("classname");
            if (path == null)
                throw new ArgumentNullException("path");

            if (path != "" && !path.EndsWith("/"))
                path = path + "/";

            string version = System.Environment.Version.ToString();

            string prefix = @"/// <summary>
/// Automatically generated by IgnoreCaseRelationGenerator for System.Environment.Version = " + version + @"
/// </summary>
    namespace " + namespacename + @"
{
internal static class " + classname + @"
{";

            string suffix = @"}
}
";
            FileInfo fi = new FileInfo(string.Format("{1}{0}.cs", classname, path));
            if (fi.Exists)
                fi.IsReadOnly = false;
            StreamWriter sw = new StreamWriter(string.Format("{1}{0}.cs", classname, path));
            sw.WriteLine(prefix);

            CreateUlongArray(sw);
            //CreateStringArray(sw);

            sw.WriteLine(suffix);
            sw.Close();
        }

        private static void CreateUlongArray(StreamWriter sw)
        {
            sw.WriteLine("/// <summary>");
            sw.WriteLine("/// Serialized BDD for mapping characters to their case-ignoring equivalence classes.");
            sw.WriteLine("/// </summary>");
            sw.WriteLine("public static ulong[] ignorecase = new ulong[]{");
            CharSetSolver solver = new CharSetSolver();

            Dictionary<char, BDD> ignoreCase = ComputeIgnoreCaseDistionary(solver);

            BDD ignorecase = solver.False;
            foreach (var kv in ignoreCase)
            {
                var a = solver.MkCharSetFromRange(kv.Key, kv.Key);
                var b = kv.Value;
                ignorecase = ignorecase.Or(a.ShiftLeft(16).And(b));
            }
            var ignorecaseArray = solver.Serialize(ignorecase);
            for (int i = 0; i < ignorecaseArray.Length; i++)
                sw.WriteLine("0x{0:X16},", ignorecaseArray[i]);

            sw.WriteLine("};"); //end of array
        }

        private static Dictionary<char, BDD> ComputeIgnoreCaseDistionary(CharSetSolver solver)
        {
            var ignoreCase = new Dictionary<char, BDD>();
            for (uint i = 0; i <= 0xFFFF; i++)
            {
                char c = (char)i;
                char cU = char.ToUpper(c); // (char.IsLetter(char.ToUpper(c)) ? char.ToUpper(c) : c);
                char cL = char.ToLower(c); // (char.IsLetter(char.ToLower(c)) ? char.ToLower(c) : c);
                if (c != cU || c != cL || cU != cL)
                {
                    //make sure that the regex engine considers c as being equivalent to cU and cL, else ignore c
                    //in some cases c != cU but the regex engine does not consider the chacarters equivalent wrt the ignore-case option.
                    //These characters are:
                    //c=\xB5,cU=\u039C
                    //c=\u0131,cU=I
                    //c=\u017F,cU=S
                    //c=\u0345,cU=\u0399
                    //c=\u03C2,cU=\u03A3
                    //c=\u03D0,cU=\u0392
                    //c=\u03D1,cU=\u0398
                    //c=\u03D5,cU=\u03A6
                    //c=\u03D6,cU=\u03A0
                    //c=\u03F0,cU=\u039A
                    //c=\u03F1,cU=\u03A1
                    //c=\u03F5,cU=\u0395
                    //c=\u1E9B,cU=\u1E60
                    //c=\u1FBE,cU=\u0399
                    if (System.Text.RegularExpressions.Regex.IsMatch(cU.ToString() + cL.ToString(), "^(?i:" + StringUtility.Escape(c) + ")+$"))
                    {
                        BDD equiv = solver.False;

                        if (ignoreCase.ContainsKey(c))
                            equiv = equiv.Or(ignoreCase[c]);
                        if (ignoreCase.ContainsKey(cU))
                            equiv = equiv.Or(ignoreCase[cU]);
                        if (ignoreCase.ContainsKey(cL))
                            equiv = equiv.Or(ignoreCase[cL]);

                        equiv = equiv.Or(solver.MkCharSetFromRange(c, c)).Or(solver.MkCharSetFromRange(cU, cU)).Or(solver.MkCharSetFromRange(cL, cL));

                        foreach (char d in solver.GenerateAllCharacters(equiv))
                            ignoreCase[d] = equiv;
                    }
                    //else
                    //{
                    //    outp += "c=" + StringUtility.Escape(c) + "," + "cU=" + StringUtility.Escape(cU);
                    //    Console.WriteLine("c=" + StringUtility.Escape(c) + "," + "cL=" + StringUtility.Escape(cL) + "," + "cU=" + StringUtility.Escape(cU));
                    //}
                }
            }
            return ignoreCase;
        }

        private static void CreateStringArray(StreamWriter sw)
        {
            sw.WriteLine("/// <summary>");
            sw.WriteLine("/// Each string correponds to an equivalence class of characters when case is ignored.");
            sw.WriteLine("/// </summary>");
            sw.WriteLine("public static string[] ignorecase = new string[]{");
            CharSetSolver solver = new CharSetSolver();

            Dictionary<char, BDD> ignoreCase = ComputeIgnoreCaseDistionary(solver);

            HashSet<BDD> done = new HashSet<BDD>();
            foreach (var kv in ignoreCase)
                if (done.Add(kv.Value))
                {
                    var ranges = kv.Value.ToRanges();
                    List<char> s = new List<char>();
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        var l = (int)ranges[i].Item1;
                        var h = (int)ranges[i].Item2;
                        for (int j = l; j <= h; j++)
                            s.Add((char)j);
                    }
                    var str = StringUtility.Escape(new String(s.ToArray()));
                    sw.WriteLine(@"{0},", str);
                }
            sw.WriteLine("};"); //end of array
        }
    };
}

