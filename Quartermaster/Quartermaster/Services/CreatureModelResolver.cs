using System;
using System.Collections.Generic;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Which fallback step in the part-resolution chain produced a model. Surfaced so the
/// caller can log the path taken (custom-content authors need to see when a fallback fired).
/// </summary>
public enum PartResolutionPath
{
    /// <summary>The creature's own race/gender/phenotype model (e.g. <c>pfe0_head001</c>).</summary>
    RaceSpecific,
    /// <summary>Same-gender human reference set (<c>pfh0</c>/<c>pmh0</c>).</summary>
    SameGenderHuman,
    /// <summary>The other gender's race-specific model (single-variant appearances).</summary>
    CrossGender,
    /// <summary>The other gender's human reference set (last resort).</summary>
    CrossGenderHuman,
}

/// <summary>Result of resolving a body-part ResRef: the chosen resref and which path found it.</summary>
public sealed record PartResolution(string ResRef, PartResolutionPath Path);

/// <summary>
/// Pure, GL-free, game-data-free resolution of part-based creature model naming (#2541 Phase 1a).
///
/// Replaces two hardcoded conventions in <see cref="ModelService"/>:
/// the <c>gender == 1 ? 'f' : 'm'</c> literal (which silently collapsed every non-1 gender to
/// male) and the human-only fallback chain in <c>AppendPart</c> (which had no cross-gender step,
/// so a single-variant appearance viewed as the other gender rendered a missing limb).
///
/// NWN ships only <c>m</c>/<c>f</c> body-model flavors — there is no <c>pb</c>/<c>po</c>/<c>pn</c>
/// prefix — so gender.2da values beyond 0/1 (2=Both, 3=Other, 4=None) map to a real flavor and
/// rely on the cross-gender fallback to find whichever variant the content actually authored.
/// </summary>
public static class CreatureModelResolver
{
    /// <summary>
    /// Resolve a gender value to the ordered list of model flavors to try: the primary flavor
    /// first, then the other gender as a cross-gender fallback. gender.2da: 0=Male, 1=Female,
    /// 2=Both, 3=Other, 4=None; only Female maps to <c>f</c>, everything else maps to <c>m</c>
    /// primary. Never throws on unknown values.
    /// </summary>
    public static IReadOnlyList<char> ResolveGenderFlavors(int gender)
        => gender == 1 ? new[] { 'f', 'm' } : new[] { 'm', 'f' };

    /// <summary>
    /// Build a creature/mannequin prefix for a flavor + race + phenotype, e.g.
    /// <c>('f', "e", 0) =&gt; "pfe0"</c>. Race is lowercased.
    /// </summary>
    public static string BuildPrefix(char flavor, string race, int phenotype)
        => $"p{flavor}{race.ToLowerInvariant()}{phenotype}";

    /// <summary>
    /// Resolve a body-part ResRef from an already-built creature prefix, trying in order:
    /// race-specific → same-gender human → cross-gender race-specific → cross-gender human.
    /// Returns the first resref for which <paramref name="modelExists"/> is true, with the path
    /// taken, or <c>null</c> if none resolve. Pure: no IO, no game-data access.
    /// </summary>
    /// <param name="prefix">The creature prefix, e.g. <c>pfe0</c> (p + flavor + race + phenotype).</param>
    /// <param name="partType">Body part type, e.g. <c>head</c>, <c>chest</c>, <c>bicepl</c>.</param>
    /// <param name="partNumber">Part variant number (1-based; 0 is handled by the caller).</param>
    /// <param name="modelExists">Predicate: does a model with this ResRef exist/load?</param>
    public static PartResolution? ResolvePart(
        string prefix, string partType, byte partNumber, Func<string, bool> modelExists)
    {
        if (string.IsNullOrEmpty(prefix) || prefix.Length < 2)
            return null;

        var flavor = prefix[1];                 // 'm' or 'f'
        var crossFlavor = flavor == 'f' ? 'm' : 'f';
        var racePheno = prefix.Substring(2);    // race + phenotype, e.g. "e0"

        // Each candidate is (resref, path); deduped so a human creature doesn't probe the human
        // reference set twice (its race step IS the human step).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (candidatePrefix, path) in EnumerateCandidates(flavor, crossFlavor, racePheno))
        {
            var resRef = MdlPartNaming.BuildBodyPartName(candidatePrefix, partType, partNumber);
            if (!seen.Add(resRef))
                continue;
            if (modelExists(resRef))
                return new PartResolution(resRef, path);
        }

        return null;
    }

    private static IEnumerable<(string Prefix, PartResolutionPath Path)> EnumerateCandidates(
        char flavor, char crossFlavor, string racePheno)
    {
        // 1. Race-specific, same gender.
        yield return ($"p{flavor}{racePheno}", PartResolutionPath.RaceSpecific);
        // 2. Same-gender human reference set.
        yield return ($"p{flavor}h0", PartResolutionPath.SameGenderHuman);
        // 3. Cross-gender, race-specific (single-variant appearances authored in one gender).
        yield return ($"p{crossFlavor}{racePheno}", PartResolutionPath.CrossGender);
        // 4. Cross-gender human reference set (last resort).
        yield return ($"p{crossFlavor}h0", PartResolutionPath.CrossGenderHuman);
    }
}
