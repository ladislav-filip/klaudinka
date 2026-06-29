namespace ClaudeAgent.Tools;

/// <summary>
/// Sandbox pro cesty file toolů — zajišťuje, že každá cílová cesta zůstává
/// uvnitř pracovního adresáře (workspace root). Brání path traversal útokům
/// (např. <c>../../.env</c>, absolutní cesty mimo root, symlinky mířící ven).
/// </summary>
public static class WorkspaceGuard
{
  /// <summary>Chybová zpráva vracená při pokusu o přístup mimo workspace.</summary>
  public const string OutsideWorkspaceError =
      "Chyba: přístup mimo pracovní adresář (workspace) není povolen.";

  // Souborový systém je na Windows case-insensitive, na Unixu case-sensitive.
  private static readonly StringComparison PathComparison =
      OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

  /// <summary>
  /// Normalizuje workspace root na absolutní cestu bez koncového oddělovače.
  /// </summary>
  public static string NormalizeRoot(string root)
  {
    if (string.IsNullOrWhiteSpace(root))
    {
      throw new ArgumentException("Workspace root nesmí být prázdný.", nameof(root));
    }

    return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
  }

  /// <summary>
  /// Ověří a vyřeší požadovanou cestu vůči workspace root. Relativní cesty se
  /// vyhodnocují vůči <paramref name="normalizedRoot"/> (ne vůči CWD).
  /// </summary>
  /// <param name="normalizedRoot">Workspace root z <see cref="NormalizeRoot"/>.</param>
  /// <param name="requestedPath">Cesta požadovaná modelem.</param>
  /// <param name="fullPath">Vyřešená absolutní cesta uvnitř workspace.</param>
  /// <param name="error">Czech chybová zpráva, pokud cesta uniká mimo workspace.</param>
  /// <returns><c>true</c>, pokud je cesta uvnitř workspace; jinak <c>false</c>.</returns>
  public static bool TryResolve(string normalizedRoot, string requestedPath, out string fullPath, out string error)
  {
    fullPath = string.Empty;
    error = string.Empty;

    // Relativní → vůči root; absolutní → pouze normalizováno.
    var resolved = Path.GetFullPath(requestedPath, normalizedRoot);

    // 1) Lexikální kontrola obsažení.
    if (!IsInside(normalizedRoot, resolved))
    {
      error = OutsideWorkspaceError;
      return false;
    }

    // 2) Best-effort obrana proti symlinkům — reálný cíl musí také zůstat uvnitř.
    var real = ResolveRealPath(resolved);
    if (!IsInside(normalizedRoot, real))
    {
      error = OutsideWorkspaceError;
      return false;
    }

    fullPath = resolved;
    return true;
  }

  /// <summary>
  /// Lexikální test, zda <paramref name="candidate"/> leží uvnitř (nebo je rovno)
  /// <paramref name="root"/>. Porovnání s koncovým oddělovačem brání záměně
  /// <c>C:\workspace-evil</c> za <c>C:\workspace</c>.
  /// </summary>
  private static bool IsInside(string root, string candidate)
  {
    if (candidate.Equals(root, PathComparison))
    {
      return true;
    }

    return candidate.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
  }

  /// <summary>
  /// Vyřeší symlinky na reálný cíl. Pro existující soubor přímo; pro neexistující
  /// cíl (zápis) vyřeší nejbližší existující nadřazený adresář a doplní zbytek cesty.
  /// Při jakékoli chybě vrací původní cestu a rozhodnutí nechá na lexikální kontrole.
  /// </summary>
  private static string ResolveRealPath(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName ?? path;
      }

      // Najdi nejbližší existující nadřazený adresář.
      var dir = Path.GetDirectoryName(path);
      while (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
      {
        dir = Path.GetDirectoryName(dir);
      }

      if (string.IsNullOrEmpty(dir))
      {
        return path;
      }

      var dirTarget = new DirectoryInfo(dir).ResolveLinkTarget(returnFinalTarget: true);
      if (dirTarget is null)
      {
        return path;
      }

      var remainder = Path.GetRelativePath(dir, path);
      return Path.GetFullPath(remainder, dirTarget.FullName);
    }
    catch
    {
      return path;
    }
  }
}
