using System.Diagnostics;
using System.Reflection;

namespace OpenPEData.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public sealed class TestFailure(string message) : Exception(message);

/// <summary>Assertions; a failed check throws TestFailure (Check collects instead of throwing).</summary>
public static class Assert
{
    public static void True(bool cond, string what)
    {
        if (!cond)
            throw new TestFailure(what);
    }

    public static void Eq(object? expected, object? actual, string what)
    {
        if (!Equals(expected, actual) && !(expected is not null && actual is not null && Runtime.Py.Eq(expected, actual)))
            throw new TestFailure($"{what}: expected {Runtime.Py.Repr(expected)}, got {Runtime.Py.Repr(actual)}");
    }

    public static T Throws<T>(Action a, string what) where T : Exception
    {
        try
        {
            a();
        }
        catch (T e)
        {
            return e;
        }
        catch (Exception e)
        {
            throw new TestFailure($"{what}: expected {typeof(T).Name}, got {e.GetType().Name}: {e.Message}");
        }
        throw new TestFailure($"{what}: expected {typeof(T).Name}, nothing thrown");
    }
}

/// <summary>Collects many sub-checks of one test; Done() throws with the first failures and the count.</summary>
public sealed class Check(string name)
{
    readonly List<string> fails = [];
    int n;

    public void Eq(object? expected, object? actual, string what)
    {
        lock (fails)
        {
            n++;
            if (!Equals(expected, actual) && !(expected is not null && actual is not null && Runtime.Py.Eq(expected, actual)))
                fails.Add($"{what}: expected {Trim(Runtime.Py.Repr(expected))}, got {Trim(Runtime.Py.Repr(actual))}");
        }
    }

    public void True(bool cond, string what) => Eq(true, cond, what);

    public void Fail(string what)
    {
        lock (fails)
        {
            n++;
            fails.Add(what);
        }
    }

    static string Trim(string s) => s.Length > 300 ? s[..300] + "..." : s;

    public int Count => n;

    public void Done()
    {
        Console.WriteLine($"    {name}: {n - fails.Count}/{n} checks passed");
        if (fails.Count > 0)
            throw new TestFailure($"{fails.Count} of {n} checks failed:\n      " + string.Join("\n      ", fails.Take(25)));
    }
}

public static class Program
{
    /// <summary>Repository root (contains dotnet\ and samples\).</summary>
    public static string Root { get; private set; } = "";

    /// <summary>Reference outputs of the Python CLI (dotnet\tools\run_cli.py &lt;dir&gt; python).</summary>
    public static string? RefDir { get; private set; }

    /// <summary>Only inputs whose path contains this text (differential and sample tests).</summary>
    public static string? Samples { get; private set; }

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !(Directory.Exists(Path.Combine(d.FullName, "dotnet")) && Directory.Exists(Path.Combine(d.FullName, "samples"))))
            d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>dotnet run --project tests\OpenPEData.Tests -c Release -- [test name filter] [--ref DIR] [--samples TEXT]</summary>
    public static int Main(string[] args)
    {
        Root = Environment.GetEnvironmentVariable("OPENPEDATA_ROOT") ?? FindRoot();
        var filter = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--ref" && i + 1 < args.Length)
                RefDir = args[++i];
            else if (args[i] == "--samples" && i + 1 < args.Length)
                Samples = args[++i];
            else
                filter.Add(args[i]);
        }
        RefDir ??= Environment.GetEnvironmentVariable("OPENPEDATA_REF");
        if (filter.Count == 2 && filter[0] == "--write-baseline")
        {
            DifferentialTests.WriteBaseline(filter[1], Path.Combine(Root, "dotnet", "tests", "OpenPEData.Tests", "baseline.json"));
            return 0;
        }
        var tests = typeof(Program).Assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
            .Where(m => filter.Count == 0 || filter.Any(f => (m.DeclaringType!.Name + "." + m.Name).Contains(f, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name).ToList();
        Console.WriteLine($"OpenPEData tests: {tests.Count} tests, root {Root}, reference {RefDir ?? "baseline.json"}");
        var failed = new List<string>();
        var total = Stopwatch.StartNew();
        foreach (var m in tests)
        {
            var name = m.DeclaringType!.Name + "." + m.Name;
            var sw = Stopwatch.StartNew();
            try
            {
                m.Invoke(null, null);
                Console.WriteLine($"  PASS {name} ({sw.Elapsed.TotalSeconds:F1} s)");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                var e = tie.InnerException;
                Console.WriteLine($"  FAIL {name} ({sw.Elapsed.TotalSeconds:F1} s)\n      {(e is TestFailure ? e.Message : e.ToString())}");
                failed.Add(name);
            }
        }
        Console.WriteLine($"{tests.Count - failed.Count} passed, {failed.Count} failed ({total.Elapsed.TotalSeconds:F0} s)");
        foreach (var f in failed)
            Console.WriteLine("  failed: " + f);
        return failed.Count == 0 ? 0 : 1;
    }
}
