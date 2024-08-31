using IntellisenseNoCopyPasta.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;
using System.CodeDom;
using Microsoft.Extensions.DependencyModel;

namespace IntellisenseNoCopyPasta.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        [HttpPost]
        public ActionResult ProcessCode(string codeContent)
        {
            // Dynamically compile the user's code
            Assembly compiledAssembly = CompileUserCode(codeContent, out List<string> compilationErrors);

            if (compiledAssembly != null)
            {
                // If compilation was successful, verify the class
                bool classExists = VerifyStaticAsyncClass(compiledAssembly, "UserNamespace.MyAsyncClass");

                if (classExists)
                {
                    ViewBag.Message = "The public static async class `MyAsyncClass` was correctly created.";
                }
                else
                {
                    ViewBag.Message = "The public static async class `MyAsyncClass` was not created correctly.";
                }
            }
            else
            {
                // If there were compilation errors, show them
                ViewBag.Message = "Compilation failed with the following errors: " + string.Join(", ", compilationErrors);
            }

            return View("CodeResult");
        }

        private Assembly CompileUserCode(string codeContent, out List<string> compilationErrors)
        {
            compilationErrors = new List<string>();

            // Define the syntax tree for the user's code
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeContent);

            // Define the compilation options
            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Reference the necessary assemblies from .NET Core runtime
            List<MetadataReference> references = GetReferences();

            // Create the compilation
            CSharpCompilation compilation = CSharpCompilation.Create(
                "UserSubmissionAssembly",
                new[] { syntaxTree },
                references,
                options);

            // Compile the code into a memory stream
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // If compilation failed, capture the errors
                    foreach (Diagnostic diagnostic in result.Diagnostics)
                    {
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                        {
                            compilationErrors.Add(diagnostic.ToString());
                        }
                    }

                    return null;
                }

                // Load the compiled assembly from the memory stream
                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
        }

        private List<MetadataReference> GetReferences()
        {
            var assemblies = new List<MetadataReference>();

            // Get all assemblies from DependencyContext (this includes all the references in the project)
            var assemblyPaths = DependencyContext.Default.CompileLibraries
                .SelectMany(cl => cl.ResolveReferencePaths())
                .Distinct();

            // Add them as metadata references
            foreach (var assemblyPath in assemblyPaths)
            {
                assemblies.Add(MetadataReference.CreateFromFile(assemblyPath));
            }

            // Add specific .NET Core assemblies that might be needed
            var coreAssemblyLocations = new[]
            {
                typeof(object).GetTypeInfo().Assembly.Location,        // System.Private.CoreLib
                typeof(Console).GetTypeInfo().Assembly.Location,       // System.Console
                typeof(Enumerable).GetTypeInfo().Assembly.Location,    // System.Linq
                typeof(Task).GetTypeInfo().Assembly.Location,          // System.Threading.Tasks
                Assembly.Load("netstandard").Location,                 // netstandard
                Assembly.Load("System.Runtime").Location               // System.Runtime (This is essential)
            };

            foreach (var location in coreAssemblyLocations)
            {
                if (!assemblies.Any(r => r.Display == location))
                {
                    assemblies.Add(MetadataReference.CreateFromFile(location));
                }
            }

            return assemblies;
        }

        private Assembly CompileUserCode(string codeContent)
        {
            // Logic to compile the codeContent into an assembly
            // This can be done using Roslyn or another C# compiler
            // For simplicity, let's assume the compilation was successful and return an assembly.
            // This method would actually return the compiled assembly from the user's submitted code.

            return Assembly.LoadFrom("UserSubmission.dll");
        }

        private bool VerifyStaticAsyncClass(Assembly assembly, string className)
        {
            Type asyncClassType = assembly.GetType(className);

            // Check if the type exists and is a public static class
            if (asyncClassType != null && asyncClassType.IsClass && asyncClassType.IsAbstract && asyncClassType.IsSealed && asyncClassType.IsPublic)
            {
                // Check for the presence of at least one async method
                MethodInfo[] methods = asyncClassType.GetMethods(BindingFlags.Public | BindingFlags.Static);

                foreach (var method in methods)
                {
                    // Check if the method is async by inspecting the return type
                    if (method.ReturnType == typeof(Task) ||
                        (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                    {
                        return true; // The class is correctly implemented as a public static async class
                    }
                }
            }

            return false; // The class is not correctly implemented
        }

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult TestView()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
