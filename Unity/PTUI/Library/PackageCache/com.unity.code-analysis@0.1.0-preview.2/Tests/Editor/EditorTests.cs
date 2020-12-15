using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NUnit.Framework;
using UnityEngine;

namespace CodeAnalysis.Tests.Editor
{
    public class EditorTests
    {
        [Test]
        public void CodeAnalysisLoadsCorrectly()
        {
            ScriptState scriptState = null;
            Task.Run(async () =>
            {
                List<MetadataReference> references = new List<MetadataReference>();
                var options = ScriptOptions.Default.WithReferences(references);
                scriptState = await CSharpScript.RunAsync("var xyz = 12; xyz", options);
        
            }).GetAwaiter().GetResult();
            
            Assert.IsTrue(scriptState != null && scriptState.Exception == null, "Code executes successfully");
        }
    }
}
