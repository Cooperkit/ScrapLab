using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScrapLab.ToolForge
{
    internal static class ToolForgeProgram
    {
        private const int AttachParentProcess = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [STAThread]
        private static int Main(string[] args)
        {
            string startupProject = String.Empty;
            if (args != null && args.Length == 2 &&
                String.Equals(args[0], "--project",
                    StringComparison.OrdinalIgnoreCase))
                startupProject = args[1];
            else if (args != null && args.Length > 0)
            {
                AttachConsole(AttachParentProcess);
                try { return RunCommand(args); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("ScrapLab Tool Forge: " + ex.Message);
                    return 1;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ToolForgeMainForm(startupProject));
            return 0;
        }

        private static int RunCommand(string[] args)
        {
            string command = args[0].Trim().ToLowerInvariant();
            if (command == "selftest")
                return ToolForgeSelfTests.Run(Console.Out) ? 0 : 1;
            string projectPath = ReadOption(args, "--project");
            if (String.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("--project <project.scraptool.json> is required.");
            projectPath = Path.GetFullPath(projectPath);
            ToolForgeProject project = ToolForgeProjectService.Load(projectPath);
            if (command == "validate")
            {
                ValidationReport report = ToolForgeValidator.Validate(project,
                    projectPath, false);
                Console.WriteLine(ToolForgeUtilities.SerializePretty(report));
                return report.Valid ? 0 : 2;
            }
            if (command == "build")
            {
                string output = ReadOption(args, "--output");
                if (String.IsNullOrWhiteSpace(output))
                    output = project.Output.BaseDirectory;
                if (String.IsNullOrWhiteSpace(output))
                    throw new ArgumentException(
                        "--output <folder> is required when the project has no output folder.");
                ToolForgeBuildResult result = SaplingPackageBuilder.Build(project,
                    projectPath, output);
                Console.WriteLine(ToolForgeUtilities.SerializePretty(result));
                return result.Success ? 0 : 2;
            }
            throw new ArgumentException(
                "Unknown command. Use validate, build, or selftest.");
        }

        private static string ReadOption(string[] args, string name)
        {
            for (int i = 1; i < args.Length - 1; i++)
                if (String.Equals(args[i], name,
                    StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return String.Empty;
        }
    }
}
