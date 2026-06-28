using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Jednotkové testy sandboxu cest <see cref="WorkspaceGuard"/>.
/// </summary>
public class WorkspaceGuardTests : IDisposable
{
    private readonly string _root;

    public WorkspaceGuardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"claude-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void NormalizeRoot_OdstraniKoncovyOddelovac()
    {
        var normalized = WorkspaceGuard.NormalizeRoot(_root + Path.DirectorySeparatorChar);

        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root)), normalized);
    }

    [Fact]
    public void NormalizeRoot_VyhodiVyjimkuProPrazdnyRoot()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceGuard.NormalizeRoot("  "));
    }

    [Fact]
    public void TryResolve_PovoliRelativniCestuUvnitr()
    {
        var root = WorkspaceGuard.NormalizeRoot(_root);

        var ok = WorkspaceGuard.TryResolve(root, "podadresar/soubor.txt", out var full, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(Path.Combine(root, "podadresar", "soubor.txt"), full);
    }

    [Fact]
    public void TryResolve_PovoliCestuRovnouRootu()
    {
        var root = WorkspaceGuard.NormalizeRoot(_root);

        var ok = WorkspaceGuard.TryResolve(root, ".", out var full, out _);

        Assert.True(ok);
        Assert.Equal(root, full);
    }

    [Fact]
    public void TryResolve_OdmitneUnikNadRootPresDvojtecku()
    {
        var root = WorkspaceGuard.NormalizeRoot(_root);

        var ok = WorkspaceGuard.TryResolve(root, Path.Combine("..", "..", "tajne.txt"), out _, out var error);

        Assert.False(ok);
        Assert.Equal(WorkspaceGuard.OutsideWorkspaceError, error);
    }

    [Fact]
    public void TryResolve_OdmitneAbsolutniCestuMimoRoot()
    {
        var root = WorkspaceGuard.NormalizeRoot(_root);
        var outside = Path.Combine(Path.GetTempPath(), "mimo-workspace.txt");

        var ok = WorkspaceGuard.TryResolve(root, outside, out _, out var error);

        Assert.False(ok);
        Assert.Equal(WorkspaceGuard.OutsideWorkspaceError, error);
    }

    [Fact]
    public void TryResolve_OdmitneSourozeneckyPrefix()
    {
        // root = ".../claude-agent-test-XXXX", sourozenec = ".../claude-agent-test-XXXX-evil"
        var root = WorkspaceGuard.NormalizeRoot(_root);
        var siblingFile = root + "-evil" + Path.DirectorySeparatorChar + "x.txt";

        var ok = WorkspaceGuard.TryResolve(root, siblingFile, out _, out var error);

        Assert.False(ok);
        Assert.Equal(WorkspaceGuard.OutsideWorkspaceError, error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
