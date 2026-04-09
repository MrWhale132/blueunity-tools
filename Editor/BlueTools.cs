using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Theblueway.Core.Runtime.Extensions;
using System.Collections.Concurrent;

namespace Theblueway.Tools.Editor
{
    public static class BlueTools
    {

        public static ConcurrentDictionary<Type, string> _typeToSourceFilePathCache = new();


        public static bool HasAsmDefFile(string path, out string asmdefPath)
        {
            var dir = Path.GetDirectoryName(path);

            var files = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly);

            if (files.Length > 0)
            {
                asmdefPath = files[0];
                return true;
            }

            asmdefPath = null;
            return false;
        }



        public static bool HasEditableSourceFile(Type type) => HasEditableSourceFile(type, out _);

        public static bool HasEditableSourceFile(Type type, out string path)
        {
            if (_typeToSourceFilePathCache.TryGetValue(type, out path))
            {
                return path != null;
            }

            path = GetSourceFilePath(type);

            return path != null;
        }


        //todo: the "Get" logic should be in a utility class, not here. The caching can stay
        public static string GetSourceFilePath(Type type)
        {
            if (_typeToSourceFilePathCache.TryGetValue(type, out var path))
            {
                return path;
            }

            var asm = type.Assembly;

            var dirsToLookIn = new List<string>();

            if (asm.GetName().Name == "Assembly-CSharp")
            {
                dirsToLookIn.Add(Application.dataPath);
            }
            else
            {
                var asmdDef = AssemblyTools.GetAsdmDefInfoInDirs(asm, AssemblyTools.EditableSourceFilesDirs);

                if (asmdDef == null) return null;

                dirsToLookIn.AddRange(asmdDef.OwnedDirectories);
            }


            var name = type.QualifiedName();


            foreach (var dir in dirsToLookIn)
            {
                var csFiles = GetCsFiles(dir);

                foreach (var csPath in csFiles)
                {
                    var inspectionReport = Roslyn.CodeGen.InspectCodeFile(csPath);


                    bool found = false;

                    foreach (var report in inspectionReport.TypeReports)
                    {

                        //debug
                        //if (report.TypeName.Contains("GenZSa") && type.Name.Contains("GenZSa"))
                        //{

                        //}

                        if (report.Namespace == type.Namespace && report.TypeName == name)
                        {
                            found = true;
                            break;
                        }
                    }

                    //debug
                    //if (csPath.Contains("Z.cs"))
                    //{

                    //}

                    if (found)
                    {
                        _typeToSourceFilePathCache[type] = csPath;
                        return csPath;
                    }
                }
            }
            Debug.LogError("didnt found source file for type: " + type.CleanAssemblyQualifiedName());
            _typeToSourceFilePathCache[type] = null;
            return null;
        }



        public static IEnumerable<string> GetCsFiles(string root)
        {

            foreach (var file in Directory.EnumerateFiles(root, "*.cs"))
                yield return file;

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (name.EndsWith("~")) continue; // skip ignored

                foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
                    yield return file;

                foreach (var subFile in GetCsFiles(dir))
                    yield return subFile;
            }
        }
    }
}
