using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Showdown.Tests.Harness
{
    /// <summary>
    /// A tiny NUnit runner: discovers [TestFixture] classes, runs [SetUp] before each
    /// [Test] / [TestCase], prints failures and exits non-zero if anything failed.
    ///
    /// Usage: ShowdownDomainTests [-v] [--include-ignored] [--filter text]
    /// </summary>
    internal static class Program
    {
        private sealed class Outcome
        {
            public string Name = "";
            public string Status = "";
            public string Detail = "";
        }

        private static int Main(string[] args)
        {
            var verbose = args.Contains("-v");
            var includeIgnored = args.Contains("--include-ignored");
            var filterIndex = Array.IndexOf(args, "--filter");
            var filter = filterIndex >= 0 && filterIndex + 1 < args.Length ? args[filterIndex + 1] : null;

            var fixtures = typeof(Program).Assembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.GetCustomAttributes(typeof(TestFixtureAttribute), true).Length > 0)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            var outcomes = new List<Outcome>();
            foreach (var fixture in fixtures)
            {
                outcomes.AddRange(RunFixture(fixture, includeIgnored, filter));
            }

            foreach (var outcome in outcomes.Where(o => o.Status == "FAIL"))
            {
                Console.WriteLine("FAIL " + outcome.Name);
                Console.WriteLine("     " + outcome.Detail.Replace("\n", "\n     "));
            }

            if (verbose)
            {
                foreach (var outcome in outcomes.Where(o => o.Status != "FAIL"))
                {
                    Console.WriteLine(outcome.Status + " " + outcome.Name + (outcome.Detail.Length > 0 ? "  (" + outcome.Detail + ")" : ""));
                }
            }

            var passed = outcomes.Count(o => o.Status == "PASS");
            var failed = outcomes.Count(o => o.Status == "FAIL");
            var ignored = outcomes.Count(o => o.Status == "SKIP");
            Console.WriteLine($"Fixtures: {fixtures.Count}  Tests: {outcomes.Count}  Passed: {passed}  Failed: {failed}  Ignored: {ignored}");

            if (outcomes.Count == 0)
            {
                Console.WriteLine("No tests were discovered.");
                return 1;
            }

            return failed == 0 ? 0 : 1;
        }

        private static IEnumerable<Outcome> RunFixture(Type fixture, bool includeIgnored, string filter)
        {
            var instance = Activator.CreateInstance(fixture)!;
            var setUps = fixture.GetMethods().Where(m => m.GetCustomAttributes(typeof(SetUpAttribute), true).Length > 0).ToList();
            var tearDowns = fixture.GetMethods().Where(m => m.GetCustomAttributes(typeof(TearDownAttribute), true).Length > 0).ToList();

            var methods = fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(m => m.MetadataToken);

            foreach (var method in methods)
            {
                var isTest = method.GetCustomAttributes(typeof(TestAttribute), true).Length > 0;
                var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().ToList();
                if (!isTest && cases.Count == 0)
                {
                    continue;
                }

                var ignore = method.GetCustomAttributes(typeof(IgnoreAttribute), true).Cast<IgnoreAttribute>().FirstOrDefault();
                var invocations = new List<object[]>();
                if (isTest && cases.Count == 0) invocations.Add(Array.Empty<object>());
                invocations.AddRange(cases.Select(c => c.Arguments ?? Array.Empty<object>()));

                foreach (var arguments in invocations)
                {
                    var name = fixture.Name + "." + method.Name
                        + (arguments.Length > 0 ? "(" + string.Join(", ", arguments.Select(Describe)) + ")" : "");
                    if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (ignore != null && !includeIgnored)
                    {
                        yield return new Outcome { Name = name, Status = "SKIP", Detail = IgnoreReason(method) };
                        continue;
                    }

                    yield return Run(instance, method, arguments, setUps, tearDowns, name);
                }
            }
        }

        /// <summary>
        /// NUnit 3.5's IgnoreAttribute.Reason getter recurses into itself, so the reason is read
        /// from the attribute's constructor argument instead.
        /// </summary>
        private static string IgnoreReason(MethodInfo method)
        {
            var data = method.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType == typeof(IgnoreAttribute));
            var reason = data != null && data.ConstructorArguments.Count > 0 ? data.ConstructorArguments[0].Value as string : null;
            return reason ?? "ignored";
        }

        private static Outcome Run(object instance, MethodInfo method, object[] arguments, List<MethodInfo> setUps, List<MethodInfo> tearDowns, string name)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                foreach (var setUp in setUps) setUp.Invoke(instance, null);
                try
                {
                    method.Invoke(instance, Coerce(method.GetParameters(), arguments));
                }
                finally
                {
                    foreach (var tearDown in tearDowns) tearDown.Invoke(instance, null);
                }

                return new Outcome { Name = name, Status = "PASS", Detail = stopwatch.ElapsedMilliseconds + " ms" };
            }
            catch (Exception exception)
            {
                var inner = exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;
                return new Outcome { Name = name, Status = "FAIL", Detail = Format(inner) };
            }
        }

        /// <summary>Attribute arguments arrive as the nearest literal type (int for a uint parameter, ...).</summary>
        private static object[] Coerce(ParameterInfo[] parameters, object[] arguments)
        {
            return arguments.Select((argument, index) =>
            {
                if (index >= parameters.Length || argument == null) return argument;
                var target = parameters[index].ParameterType;
                if (target.IsInstanceOfType(argument) || target.IsEnum || !typeof(IConvertible).IsAssignableFrom(target)) return argument;
                return Convert.ChangeType(argument, target, System.Globalization.CultureInfo.InvariantCulture);
            }).ToArray();
        }

        private static string Format(Exception exception)
        {
            var frames = new StackTrace(exception, true).GetFrames() ?? Array.Empty<StackFrame>();
            var where = frames
                .Select(frame => (file: frame.GetFileName(), line: frame.GetFileLineNumber()))
                .FirstOrDefault(frame => frame.file != null && frame.file.Contains("/Tests/Domain/"));
            var location = where.file != null ? "\nat " + where.file + ":" + where.line : "";
            return exception.GetType().Name + ": " + exception.Message.Trim() + location;
        }

        private static string Describe(object argument) =>
            argument is string text ? "\"" + text + "\"" : Convert.ToString(argument, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
    }
}
